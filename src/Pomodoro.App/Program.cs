using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Labs.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pomodoro.App.Services;
using Pomodoro.App.ViewModels;
using Pomodoro.App.Views;
using Pomodoro.Domain.Interfaces;
using Pomodoro.Infrastructure;
using Pomodoro.Infrastructure.Persistence;
using Serilog;

namespace Pomodoro.App;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Parse CLI args early
        var minimized = Array.Exists(args, a => a == "--minimized");

        // Build DI container
        var host = BuildHost(args);
        ServiceLocator.SetServiceProvider(host.Services);

        // Initialize SQLite schema (CREATE TABLE IF NOT EXISTS) before anything reads/writes
        try
        {
            var sqliteCtx = host.Services.GetRequiredService<SqliteDbContext>();
            sqliteCtx.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to initialize SQLite database");
            throw;
        }

        // Hook the activity alert → notification
        WireActivityAlert(host.Services);

        var shutdownCts = new CancellationTokenSource();
        var exitCode = 0;

        try
        {
            Log.Information("Starting Avalonia desktop lifetime");
            BuildAvaloniaApp(host.Services, minimized, shutdownCts).StartWithClassicDesktopLifetime(args);
            Log.Information("Avalonia desktop lifetime exited");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            exitCode = 1;
        }
        finally
        {
            // Cleanup must NOT run on the main thread: even after the Avalonia loop
            // exits, this thread still carries Avalonia's SynchronizationContext, and
            // any await inside host disposal posts its continuation to the stopped
            // dispatcher — deadlocking forever (the "process lingers in Task Manager"
            // bug). A thread-pool thread has no sync context, so continuations run inline.
            Task.Run(() => Shutdown(shutdownCts, host, exitCode)).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Runs after the Avalonia loop exits, on a thread-pool thread.
    /// A watchdog force-exits the process if cleanup ever deadlocks again.
    /// </summary>
    private static void Shutdown(CancellationTokenSource shutdownCts, IHost host, int exitCode)
    {
        using var watchdog = new Timer(
            _ => Environment.Exit(exitCode),
            null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);

        Log.Information("Shutdown: begin");

        // Signal background loops to stop
        try { shutdownCts.Cancel(); }
        catch (Exception ex) { Log.Warning(ex, "Shutdown signal failed"); }
        shutdownCts.Dispose();
        Log.Information("Shutdown: cancellation signalled");

        // Dispose services synchronously (stops hooks, flushes channels, closes DB)
        try
        {
            if (host is IAsyncDisposable asyncHost)
                asyncHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
            else
                host.Dispose();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error during host disposal");
            exitCode = 1;
        }
        Log.Information("Shutdown: host disposed");

        Log.CloseAndFlush();

        // Hard-exit so stray native threads (e.g. SharpHook's hook thread) can't
        // keep the process alive after cleanup.
        Environment.Exit(exitCode);
    }

    private static IHost BuildHost(string[] args)
    {
        var appDataDir = GetAppDataDirectory();
        var dbPath = Path.Combine(appDataDir, "pomodoro.db");
        var logsDir = Path.Combine(appDataDir, "logs");
        var soundsDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");

        var serilogLogger = Pomodoro.Infrastructure.Logging.LoggingConfigurator.Configure(logsDir);
        Log.Logger = serilogLogger;

        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((ctx, services) =>
            {
                services.AddLogging(b => b.AddSerilog(serilogLogger, dispose: false));
                services.AddPomodoroInfrastructure(dbPath, soundsDir);

                // App-layer services
                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<TaskListViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<DailyReportViewModel>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<ITickScheduler, DispatcherTimerTickScheduler>();
            })
            .Build();

        return host;
    }

    private static AppBuilder BuildAvaloniaApp(IServiceProvider services, bool startMinimized, CancellationTokenSource shutdownCts)
    {
        var builder = AppBuilder.Configure(() => new App(services, startMinimized, shutdownCts))
            .UsePlatformDetect()
            .WithAppNotifications(new AppNotificationOptions
            {
                // The COM activator is only needed for toast click callbacks,
                // which this app doesn't use — and its registration NREs on
                // some machines. Skip it; toasts still show normally.
                DisableComServer = true,
                AppName = "Shahsavar Pomodoro 2500",
            })
            .LogToTrace();

        return builder;
    }

    private static void WireActivityAlert(IServiceProvider services)
    {
        var evaluator = services.GetRequiredService<IActivityAlertEvaluator>();
        var notifications = services.GetRequiredService<INotificationService>();

        evaluator.AlertRaised += async (_, e) =>
        {
            try { await notifications.ShowOverActivityAlertAsync(e.Message); }
            catch { /* swallow */ }
        };
    }

    public static void StartEngineTickLoop(IServiceProvider services, CancellationToken shutdownToken)
    {
        _ = Task.Run(async () =>
        {
            var engine = services.GetRequiredService<IPomodoroEngine>();
            var tickScheduler = services.GetRequiredService<ITickScheduler>();

            // Restore persisted state (cycle counter) before starting ticks
            try { await engine.InitializeAsync(); }
            catch (Exception ex) { Log.Warning(ex, "Engine initialization failed — continuing with defaults"); }

            tickScheduler.Tick += async (_, _) =>
            {
                try { await engine.OnSecondTickAsync(); }
                catch (Exception ex) { Log.Warning(ex, "Engine tick failed"); }
            };

            tickScheduler.Start();

            // Wait until shutdown is requested instead of blocking forever
            try { await Task.Delay(Timeout.Infinite, shutdownToken); }
            catch (OperationCanceledException) { /* expected on shutdown */ }

            tickScheduler.Stop();
            Log.Information("Engine tick loop stopped");
        }, CancellationToken.None);
    }

    private static string GetAppDataDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = Path.Combine(baseDir, "Pomodoro");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return dir;
    }
}

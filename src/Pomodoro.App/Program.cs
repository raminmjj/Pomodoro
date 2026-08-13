using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
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

        try
        {
            BuildAvaloniaApp(host.Services, minimized).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
            host.Dispose();
        }
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

    private static AppBuilder BuildAvaloniaApp(IServiceProvider services, bool startMinimized)
    {
        var builder = AppBuilder.Configure(() => new App(services, startMinimized))
            .UsePlatformDetect()
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

    // این متد حالا از App فراخوانی می‌شود (بعد از آماده شدن Dispatcher)
    public static void StartEngineTickLoop(IServiceProvider services)
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
            await Task.Delay(Timeout.Infinite);
        });
    }

    private static string GetAppDataDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = Path.Combine(baseDir, "Pomodoro");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return dir;
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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

        // Single-instance guard: if another instance is already running,
        // tell it to restore from the tray and exit immediately. This avoids
        // MicroCom's broken AOT COM interop (Release(Ccw*) NullRef).
        const string mutexName = "Global\\Pomodoro_SingleInstance";
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                SignalRestore();
                return;
            }
        }
        catch
        {
            // Mutex creation can fail in restricted environments — continue as single instance.
        }

        // Register LocalServer32 so Windows starts the exe when a toast is
        // clicked. The new process will detect the running instance above and
        // send a restore signal instead of launching a second copy.
        if (OperatingSystem.IsWindows())
            RegisterLocalServer(GetComServerGuid());

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
        // The notification COM server must be released before the host teardown —
        // otherwise MicroCom's CCW release races with the dispatcher shutdown and
        // throws NullReferenceException in Release(Ccw*).
        try
        {
            if (Avalonia.Labs.Notifications.NativeNotificationManager.Current is IDisposable notifDisp)
                notifDisp.Dispose();
        }
        catch (Exception ex) { Log.Warning(ex, "Notification manager disposal failed"); }

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
        var appDataDir = GetAppDataDirectory(out var usedFallback);
        var dbPath = Path.Combine(appDataDir, "pomodoro.db");
        var logsDir = Path.Combine(appDataDir, "logs");
        var soundsDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");

        var serilogLogger = Pomodoro.Infrastructure.Logging.LoggingConfigurator.Configure(logsDir);
        Log.Logger = serilogLogger;
        if (usedFallback)
            Log.Warning("App data directory not writable ({BaseDir}); using per-user data directory {DataDir}",
                AppContext.BaseDirectory, appDataDir);

        // A stale non-SQLite file (e.g. from the pre-SQLite LiteDB era) at the
        // data path crashes startup with "file is not a database". Detect it by
        // header and move it aside so a fresh database can be created.
        EnsureDatabaseFileIsValid(dbPath);

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
            .LogToTrace();

        if (!OperatingSystem.IsLinux())
        {
            // Avalonia.Labs.Notifications 12.0.2 is compiled against
            // Tmds.DBus.Protocol 0.92.0 (class Connection), but the dependency
            // graph resolves 0.94.1 (required by Avalonia.FreeDesktop 12.1.1),
            // where Connection was renamed to DBusConnection. Creating the
            // Linux native manager therefore throws TypeLoadException at
            // startup. Linux notifications are handled by
            // FreeDesktopNotifications instead; the Labs manager is still used
            // on Windows (WinRT toasts) and macOS (Notification Center).
            builder = builder.WithAppNotifications(new AppNotificationOptions
            {
                // The COM activator is required for toast click callbacks:
                // clicking a notification restores the window from the tray.
                // DISABLED: MicroCom's AOT code generation doesn't track the
                // NotificationActivator CCW properly — the COM runtime's
                // Release(Ccw*) finds a null pointer and crashes. The tray
                // icon's Open menu provides the same restore functionality.
                DisableComServer = true,
                AppName = "Shahsavar Pomodoro 2500",
                // A stable AUMID + icon is what makes Windows show the app's
                // icon (instead of the generic one) in toasts and in the
                // notification settings page. Without it, Avalonia.Labs
                // synthesizes an unregistered AUMID from the exe name.
                AppUserModelId = "ShahsavarPomodoro.Pomodoro",
                AppIcon = Path.Combine(AppContext.BaseDirectory, "app.ico"),
            });
        }

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

    private static string GetAppDataDirectory(out bool usedFallback)
    {
        usedFallback = false;

        // Portable-first: store data next to the executable so the app can be
        // carried around as a folder. If the location is not writable (e.g. the
        // deb/rpm packages install to /opt/pomodoro, which is root-owned and
        // read-only), fall back to the per-user data directory.
        var baseDir = AppContext.BaseDirectory;
        var dir = Path.Combine(baseDir, "PomodoroData");
        try
        {
            Directory.CreateDirectory(dir);
            // Probe writability (CreateDirectory is a no-op on existing dirs).
            using (File.Create(Path.Combine(dir, ".write-test"), 1, FileOptions.DeleteOnClose)) { }
            return dir;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "App data directory not writable ({Dir}); falling back to per-user data directory", dir);
            usedFallback = true;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var fallback = string.IsNullOrEmpty(localAppData) ? dir : Path.Combine(localAppData, "Pomodoro");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private static void EnsureDatabaseFileIsValid(string dbPath)
    {
        if (!File.Exists(dbPath)) return;
        try
        {
            using var fs = File.OpenRead(dbPath);
            if (fs.Length >= 16)
            {
                Span<byte> header = stackalloc byte[16];
                fs.ReadExactly(header);
                if (header.SequenceEqual("SQLite format 3\0"u8)) return;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not inspect existing database file {Path}", dbPath);
            return;
        }

        var backup = dbPath + ".invalid.bak";
        File.Move(dbPath, backup, overwrite: true);
        Log.Warning("Existing database file {Path} is not a valid SQLite database; moved to {Backup}", dbPath, backup);
    }

    // ──────────────────────────────────────────────────────────────
    //  Single-instance IPC (named pipe + LocalServer32)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Compute the COM server GUID from the AUMID — must match
    /// <c>AumidHelper.GetGuidFromId</c> so the CLSID registry entry
    /// aligns with what the toast infrastructure expects.
    /// </summary>
    private static Guid GetComServerGuid()
    {
        const string aumid = "ShahsavarPomodoro.Pomodoro";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(aumid));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
    }

    /// <summary>
    /// Register HKCU\Software\Classes\CLSID\{guid}\LocalServer32 so that
    /// Windows starts the exe when a notification is clicked.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RegisterLocalServer(Guid comGuid)
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            var keyPath = $@"Software\Classes\CLSID\{{{comGuid}}}\LocalServer32";
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath);
            key?.SetValue(null, exePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to register LocalServer32 for notification click-to-restore");
        }
    }

    /// <summary>
    /// Connect to the running instance's named pipe and send a restore signal.
    /// Called by the second process (started by Windows on notification click).
    /// </summary>
    private static void SignalRestore()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", "Pomodoro_Restore", PipeDirection.Out);
            client.Connect(3000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine("restore");
        }
        catch
        {
            // Pipe not available — the running instance may have exited.
        }
    }

    /// <summary>
    /// Start listening for restore signals from second instances on a
    /// background thread. Must be called after the Avalonia dispatcher
    /// is running.
    /// </summary>
    public static void StartRestoreListener()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        "Pomodoro_Restore", PipeDirection.In,
                        1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server);
                    var msg = await reader.ReadLineAsync();
                    if (msg == "restore")
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (Avalonia.Application.Current?.ApplicationLifetime
                                is IClassicDesktopStyleApplicationLifetime desktop)
                            {
                                if (desktop.MainWindow is MainWindow mw)
                                    mw.RestoreFromTray();
                            }
                        });
                    }
                }
                catch
                {
                    // Pipe error — wait a moment and retry.
                    await Task.Delay(1000);
                }
            }
        }, CancellationToken.None);
    }
}

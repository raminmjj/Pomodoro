using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Infrastructure.Autostart;

/// <summary>
/// Linux autostart via systemd --user service. Falls back to XDG autostart
/// .desktop file if systemd is unavailable.
/// </summary>
public sealed class LinuxAutoStartService : IAutoStartService
{
    private const string ServiceName = "pomodoro";
    private readonly ILogger<LinuxAutoStartService> _logger;

    public LinuxAutoStartService(ILogger<LinuxAutoStartService> logger) => _logger = logger;

    private static string ServicePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "systemd", "user", $"{ServiceName}.service");

    private static string XdgDesktopPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "autostart", $"{ServiceName}.desktop");

    public Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        var systemdEnabled = File.Exists(ServicePath) && IsSystemdEnabled();
        var xdgEnabled = File.Exists(XdgDesktopPath);
        return Task.FromResult(systemdEnabled || xdgEnabled);
    }

    public async Task EnableAsync(CancellationToken ct = default)
    {
        try
        {
            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;

            // Try systemd --user first
            if (HasSystemdUser())
            {
                var dir = Path.GetDirectoryName(ServicePath)!;
                Directory.CreateDirectory(dir);
                var content = $"""
                    [Unit]
                    Description=Pomodoro Productivity App
                    After=graphical-session.target

                    [Service]
                    Type=simple
                    ExecStart={exePath} --minimized
                    Restart=on-failure
                    RestartSec=5

                    [Install]
                    WantedBy=default.target
                    """;
                await File.WriteAllTextAsync(ServicePath, content, ct);
                RunShell("daemon-reload");
                RunShell($"enable {ServiceName}.service");
                _logger.LogInformation("Autostart enabled via systemd --user: {Path}", ServicePath);
                return;
            }

            // Fallback: XDG autostart
            var xdgDir = Path.GetDirectoryName(XdgDesktopPath)!;
            Directory.CreateDirectory(xdgDir);
            var desktop = $"""
                [Desktop Entry]
                Type=Application
                Name=Pomodoro
                Comment=Cross-platform Pomodoro timer
                Exec={exePath} --minimized
                Icon=pomodoro
                Terminal=false
                X-GNOME-Autostart-enabled=true
                """;
            await File.WriteAllTextAsync(XdgDesktopPath, desktop, ct);
            _logger.LogInformation("Autostart enabled via XDG: {Path}", XdgDesktopPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable autostart");
        }
    }

    public Task DisableAsync(CancellationToken ct = default)
    {
        try
        {
            if (File.Exists(ServicePath))
            {
                RunShell($"disable {ServiceName}.service");
                File.Delete(ServicePath);
                RunShell("daemon-reload");
                _logger.LogInformation("Autostart disabled (systemd)");
            }
            if (File.Exists(XdgDesktopPath))
            {
                File.Delete(XdgDesktopPath);
                _logger.LogInformation("Autostart disabled (XDG)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disable autostart");
        }
        return Task.CompletedTask;
    }

    private static bool HasSystemdUser()
    {
        try
        {
            var psi = new ProcessStartInfo("systemctl", "--user is-system-running")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(1000);
            return p?.ExitCode == 0 || p?.ExitCode == 1;  // 1 = degraded but running
        }
        catch { return false; }
    }

    private static bool IsSystemdEnabled()
    {
        try
        {
            var psi = new ProcessStartInfo("systemctl", $"--user is-enabled {ServiceName}.service")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(1000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static void RunShell(string args)
    {
        try
        {
            var p = Process.Start("systemctl", $"--user {args}");
            p?.WaitForExit(2000);
        }
        catch { /* ignore */ }
    }
}

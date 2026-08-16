using System.Runtime.Versioning;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Infrastructure.Autostart;

/// <summary>
/// Windows autostart via HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run.
/// No admin privilege required.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Shahsavar Pomodoro 2500";
    private const string LegacyAppName = "Pomodoro"; // pre-rename value name
    private readonly ILogger<WindowsAutoStartService> _logger;

    public WindowsAutoStartService(ILogger<WindowsAutoStartService> logger) => _logger = logger;

    public Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) return Task.FromResult(false);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            var value = key?.GetValue(AppName) as string;
            return Task.FromResult(value is not null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read autostart registry key");
            return Task.FromResult(false);
        }
    }

    public Task EnableAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) return Task.CompletedTask;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
            key?.SetValue(AppName, $"\"{exePath}\" --minimized");
            // remove the pre-rename entry so autostart doesn't fire twice
            key?.DeleteValue(LegacyAppName, throwOnMissingValue: false);
            _logger.LogInformation("Autostart enabled: {Path}", exePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable autostart");
        }
        return Task.CompletedTask;
    }

    public Task DisableAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) return Task.CompletedTask;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
            key?.DeleteValue(LegacyAppName, throwOnMissingValue: false);
            _logger.LogInformation("Autostart disabled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disable autostart");
        }
        return Task.CompletedTask;
    }
}

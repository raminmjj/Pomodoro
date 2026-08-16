using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Infrastructure.Autostart;

/// <summary>
/// macOS autostart via LaunchAgent plist in ~/Library/LaunchAgents/.
/// Loaded into launchd with `launchctl load`.
/// </summary>
public sealed class MacAutoStartService : IAutoStartService
{
    private const string Label = "com.shahsavar.pomodoro2500";
    private readonly ILogger<MacAutoStartService> _logger;

    public MacAutoStartService(ILogger<MacAutoStartService> logger) => _logger = logger;

    private static string PlistPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents", $"{Label}.plist");

    public Task<bool> IsEnabledAsync(CancellationToken ct = default) =>
        Task.FromResult(File.Exists(PlistPath));

    public Task EnableAsync(CancellationToken ct = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(PlistPath)!;
            Directory.CreateDirectory(dir);

            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
            // Use a raw string template — much simpler than building XElement tree
            var plistXml = $""""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>{Label}</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{exePath}</string>
                        <string>--minimized</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                    <key>LaunchOnlyOnce</key>
                    <true/>
                </dict>
                </plist>
                """";

            File.WriteAllText(PlistPath, plistXml);
            RunShell($"load \"{PlistPath}\"");
            _logger.LogInformation("Autostart enabled via LaunchAgent: {Path}", PlistPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable autostart");
        }
        return Task.CompletedTask;
    }

    public Task DisableAsync(CancellationToken ct = default)
    {
        try
        {
            if (File.Exists(PlistPath))
            {
                RunShell($"unload \"{PlistPath}\"");
                File.Delete(PlistPath);
                _logger.LogInformation("Autostart disabled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disable autostart");
        }
        return Task.CompletedTask;
    }

    private static void RunShell(string args)
    {
        try
        {
            var p = Process.Start("launchctl", args);
            p?.WaitForExit(2000);
        }
        catch { /* ignore */ }
    }
}

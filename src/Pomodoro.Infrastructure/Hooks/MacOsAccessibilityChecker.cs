using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Pomodoro.Infrastructure.Hooks;

/// <summary>
/// Checks and prompts for macOS Accessibility permissions required by SharpHook.
/// On non-macOS platforms, this is a no-op.
/// </summary>
public static class MacOsAccessibilityChecker
{
    /// <summary>
    /// Returns true if accessibility permissions are granted (or not applicable).
    /// On macOS, checks AXIsProcessTrusted(). If not trusted, logs a warning
    /// and optionally opens System Preferences.
    /// </summary>
    public static bool EnsureAccessibilityPermission(ILogger logger, bool promptUser = true)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return true; // Not macOS — no permission needed

        try
        {
            // Check if we have accessibility permission via CoreGraphics
            var trusted = NativeMethods.AXIsProcessTrusted();
            if (trusted)
            {
                logger.LogInformation("macOS accessibility permission is granted");
                return true;
            }

            logger.LogWarning("macOS accessibility permission is NOT granted. Global hooks will not work.");

            if (promptUser)
            {
                logger.LogInformation("Opening System Preferences > Accessibility...");
                // Open System Settings to the Accessibility pane
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility",
                    UseShellExecute = false,
                });
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check macOS accessibility permission");
            return false;
        }
    }

    /// <summary>
    /// P/Invoke declarations for macOS ApplicationServices framework.
    /// These are only called on macOS.
    /// </summary>
    private static class NativeMethods
    {
        [DllImport("ApplicationServices", EntryPoint = "AXIsProcessTrusted")]
        public static extern bool AXIsProcessTrusted();
    }
}

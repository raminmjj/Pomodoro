using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Pomodoro.Domain.Enums;
using Pomodoro.Infrastructure.Notifications;

namespace Pomodoro.App.Services;

/// <summary>
/// Shows OS-level desktop notifications via Avalonia.Labs.Notifications
/// (WinRT toasts on Windows, Notification Center on macOS, D-Bus on Linux).
/// On Linux, the portal-based Avalonia.Labs path is replaced with the classic
/// org.freedesktop.Notifications D-Bus interface, which is implemented by all
/// desktop shells (including GNOME Shell 3.36 on Ubuntu 20.04). Falls back to
/// an in-app WindowNotificationManager toast when no native path is available.
/// </summary>
internal sealed class DesktopNotificationSink : INotificationSink
{
    private const string IconResource = "avares://Pomodoro/Assets/appicon.png";
    private const string AppIconName = "com.pomodoro.Pomodoro";

    private readonly Window _window;
    private readonly Bitmap? _icon;
    private readonly FreeDesktopNotifications? _freeDesktopNotifications;
    private WindowNotificationManager? _fallback;

    /// <summary>
    /// Raised when the user clicks on a desktop notification.
    /// </summary>
    public event Action? NotificationActivated;

    public DesktopNotificationSink(Window window)
    {
        _window = window;
        _icon = LoadIcon();

        if (OperatingSystem.IsLinux())
        {
            // Avalonia.Labs.Notifications only talks to the XDG desktop portal
            // (org.freedesktop.portal.Notification) on Linux, which is not
            // implemented by older desktops (e.g. GNOME Shell < 40 on
            // Ubuntu 20.04) and fails silently. Use the classic interface.
            _freeDesktopNotifications = new FreeDesktopNotifications();
            _freeDesktopNotifications.Activated += () => NotificationActivated?.Invoke();
        }

        // Subscribe to notification completed event to detect user clicks
        try
        {
            var manager = Avalonia.Labs.Notifications.NativeNotificationManager.Current;
            if (manager != null)
            {
                manager.NotificationCompleted += (_, e) =>
                {
                    if (e.IsActivated)
                    {
                        NotificationActivated?.Invoke();
                    }
                };
            }
        }
        catch
        {
            // Ignore - native notifications not available
        }
    }

    private static Bitmap? LoadIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(IconResource));
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    public void Show(string title, string body, NotificationSeverity severity)
    {
        if (_freeDesktopNotifications is not null)
        {
            _ = ShowFreeDesktopAsync(title, body, severity);
            return;
        }

        try
        {
            var manager = Avalonia.Labs.Notifications.NativeNotificationManager.Current;
            var notification = manager?.CreateNotification(category: null);
            if (notification is not null)
            {
                notification.Title = title;
                notification.Message = body;
                notification.Icon = _icon;
                notification.Expiration = TimeSpan.FromSeconds(10);
                notification.Show();
                return;
            }
        }
        catch
        {
            // native path failed — fall through to the in-app toast
        }

        ShowFallback(title, body, severity);
    }

    private async Task ShowFreeDesktopAsync(string title, string body, NotificationSeverity severity)
    {
        try
        {
            if (await _freeDesktopNotifications!.ShowAsync(title, body, AppIconName))
            {
                return;
            }
        }
        catch
        {
            // D-Bus unavailable — fall through to the in-app toast
        }

        ShowFallback(title, body, severity);
    }

    private void ShowFallback(string title, string body, NotificationSeverity severity)
    {
        _fallback ??= new WindowNotificationManager(_window)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 3,
        };

        var type = severity switch
        {
            NotificationSeverity.Success => NotificationType.Success,
            NotificationSeverity.Warning => NotificationType.Warning,
            NotificationSeverity.Error => NotificationType.Error,
            _ => NotificationType.Information,
        };
        _fallback.Show(new Notification(title, body, type));
    }
}

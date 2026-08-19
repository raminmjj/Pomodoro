using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Pomodoro.App.Services;

/// <summary>
/// Sends notifications over the classic org.freedesktop.Notifications D-Bus
/// interface, implemented by every major Linux desktop (GNOME Shell since 3.36,
/// KDE Plasma, XFCE). Unlike the XDG desktop portal path used by
/// Avalonia.Labs.Notifications on Linux, this interface has existed for
/// decades, so it works on Ubuntu 20.04 and other older distributions.
/// </summary>
internal sealed class FreeDesktopNotifications
{
    public const string Service = "org.freedesktop.Notifications";
    public const string ObjectPath = "/org/freedesktop/Notifications";
    public const string InterfaceName = "org.freedesktop.Notifications";

    private const string DefaultAction = "default";
    private const int ExpireTimeoutMs = 10_000;

    private readonly object _watchGate = new();
    private IDisposable? _actionInvokedWatch;
    private bool _watchStarted;

    /// <summary>
    /// Raised when the user clicks on a notification. The handler runs on the
    /// SynchronizationContext that was current when the first notification was
    /// shown (the UI thread).
    /// </summary>
    public event Action? Activated;

    /// <summary>
    /// Sends a notification. Never throws — returns false when the session bus
    /// or the notification service is unavailable so the caller can fall back.
    /// </summary>
    public async Task<bool> ShowAsync(string title, string body, string? iconName)
    {
        try
        {
            EnsureActionInvokedWatch();

            MessageBuffer message;
            using (var writer = DBusConnection.Session.GetMessageWriter())
            {
                writer.WriteMethodCallHeader(Service, ObjectPath, InterfaceName, "Notify", "susssasa{sv}i");
                writer.WriteString("Pomodoro");
                writer.WriteUInt32(0);
                writer.WriteString(iconName ?? string.Empty);
                writer.WriteString(title);
                writer.WriteString(body);
                writer.WriteArray(new[] { DefaultAction, "Open Pomodoro" });
                writer.WriteDictionary(new Dictionary<string, VariantValue>());
                writer.WriteInt32(ExpireTimeoutMs);
                message = writer.CreateMessage();
            }

            await DBusConnection.Session.CallMethodAsync<uint>(
                message,
                static (message, _) => message.GetBodyReader().ReadUInt32());
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void EnsureActionInvokedWatch()
    {
        if (_watchStarted)
        {
            return;
        }

        lock (_watchGate)
        {
            if (_watchStarted)
            {
                return;
            }

            _watchStarted = true;
            try
            {
                _actionInvokedWatch = DBusConnection.Session.WatchSignalAsync<(uint, string)>(
                    Service,
                    ObjectPath,
                    InterfaceName,
                    "ActionInvoked",
                    static (message, _) =>
                    {
                        var reader = message.GetBodyReader();
                        return (reader.ReadUInt32(), reader.ReadString());
                    },
                    notification =>
                    {
                        if (notification.HasValue && notification.Value.Item2 == DefaultAction)
                        {
                            Activated?.Invoke();
                        }
                    },
                    ObserverFlags.None).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Click-to-restore is best effort; the watch is retried on the
                // next ShowAsync call.
                _watchStarted = false;
            }
        }
    }
}

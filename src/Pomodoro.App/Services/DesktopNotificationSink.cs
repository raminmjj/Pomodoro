using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Pomodoro.Domain.Enums;
using Pomodoro.Infrastructure.Notifications;

namespace Pomodoro.App.Services;

/// <summary>
/// Shows OS-level desktop notifications:
///   - Windows: Shell_NotifyIcon balloon (rendered as a native toast on Win10/11)
///   - macOS:   Notification Center via osascript
///   - Linux:   notify-send (FreeDesktop)
/// Falls back to an in-app WindowNotificationManager toast when the OS path
/// fails (e.g. notify-send not installed). Pure P/Invoke and subprocess calls —
/// no external packages, NativeAOT-safe.
/// </summary>
internal sealed class DesktopNotificationSink : INotificationSink
{
    private const uint TrayIconId = 0x5C1E;

    private readonly Window _window;
    private WindowNotificationManager? _fallback;

    public DesktopNotificationSink(Window window) => _window = window;

    public void Show(string title, string body, NotificationSeverity severity)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Show(title, body, severity));
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows()) { ShowWindows(title, body, severity); return; }
            if (OperatingSystem.IsMacOS()) { ShowMacOs(title, body); return; }
            if (OperatingSystem.IsLinux()) { ShowLinux(title, body, severity); return; }
        }
        catch (Exception)
        {
            // OS notification path failed — fall through to the in-app toast
        }

        ShowFallback(title, body, severity);
    }

    // ── Windows ──────────────────────────────────────────────────────

    private void ShowWindows(string title, string body, NotificationSeverity severity)
    {
        var handle = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) throw new InvalidOperationException("No window handle");

        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = handle,
            uID = TrayIconId,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_INFO,
            uCallbackMessage = WM_APP,
            hIcon = LoadIconW(IntPtr.Zero, IDI_APPLICATION),
            szTip = "Shahsavar Pomodoro 2500",
            szInfo = body ?? string.Empty,
            szInfoTitle = title ?? string.Empty,
            dwInfoFlags = severity switch
            {
                NotificationSeverity.Warning => NIIF_WARNING,
                NotificationSeverity.Error => NIIF_ERROR,
                _ => NIIF_INFO,
            },
        };

        // NIM_ADD with NIF_INFO adds a hidden tray icon and immediately
        // raises the balloon; Explorer removes the toast once dismissed.
        if (!Shell_NotifyIconW(NIM_ADD, ref data))
            throw new InvalidOperationException("Shell_NotifyIcon failed");

        // Remove the tray icon again so nothing lingers in the notification area.
        _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
            Dispatcher.UIThread.Post(() =>
            {
                var remove = new NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = handle,
                    uID = TrayIconId,
                };
                Shell_NotifyIconW(NIM_DELETE, ref remove);
            }));
    }

    private const uint NIM_ADD = 0x0;
    private const uint NIM_DELETE = 0x2;
    private const uint NIF_MESSAGE = 0x1;
    private const uint NIF_ICON = 0x2;
    private const uint NIF_INFO = 0x10;
    private const uint NIIF_INFO = 0x0;
    private const uint NIIF_WARNING = 0x2;
    private const uint NIIF_ERROR = 0x3;
    private const uint WM_APP = 0x8000;
    private static readonly IntPtr IDI_APPLICATION = new(32512);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr iconId);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
    }

    // ── macOS ────────────────────────────────────────────────────────

    private static void ShowMacOs(string title, string body)
    {
        var script = $"display notification \"{Escape(body)}\" with title \"{Escape(title)}\"";
        StartProcess("/usr/bin/osascript", "-e", script);
    }

    // ── Linux ────────────────────────────────────────────────────────

    private static void ShowLinux(string title, string body, NotificationSeverity severity)
    {
        var urgency = severity switch
        {
            NotificationSeverity.Warning or NotificationSeverity.Error => "critical",
            _ => "normal",
        };
        StartProcess("notify-send", "-u", urgency, title, body);
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static string Escape(string? s) =>
        (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void StartProcess(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = args[0],
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        for (int i = 1; i < args.Length; i++)
            psi.ArgumentList.Add(args[i]);

        using var _ = Process.Start(psi);
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

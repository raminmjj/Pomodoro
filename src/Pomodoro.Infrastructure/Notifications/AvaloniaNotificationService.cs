using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Infrastructure.Notifications;

/// <summary>
/// Wraps Avalonia's WindowNotificationManager into our domain interface.
/// Avalonia.Native is platform-aware (Windows Toast, macOS NSUserNotification,
/// Linux FreeDesktop D-Bus).
///
/// The actual Avalonia WindowNotificationManager is created by the App layer
/// after the main window is shown — see Initialize.
/// </summary>
public sealed class AvaloniaNotificationService : INotificationService
{
    private readonly ILogger<AvaloniaNotificationService> _logger;
    private INotificationSink? _sink;

    public AvaloniaNotificationService(ILogger<AvaloniaNotificationService> logger)
    {
        _logger = logger;
    }

    /// <summary>Inject the Avalonia-specific sink (created by the App layer).</summary>
    public void Initialize(INotificationSink sink)
    {
        _sink = sink;
        _logger.LogInformation("Notification sink initialized");
    }

    public Task ShowAsync(string title, string body, NotificationSeverity severity = NotificationSeverity.Information, CancellationToken ct = default)
    {
        if (_sink is null)
        {
            _logger.LogWarning("Notification sink not initialized — dropping: {Title}", title);
            return Task.CompletedTask;
        }

        _sink.Show(title, body, severity);
        return Task.CompletedTask;
    }

    public Task ShowBreakStartAsync(int breakMinutes, bool isLongBreak, CancellationToken ct = default) =>
        ShowAsync(
            isLongBreak ? "Long Break" : "Break Time",
            $"Take a {breakMinutes}-minute break. Step away from the screen.",
            NotificationSeverity.Success, ct);

    public Task ShowFocusStartAsync(int focusMinutes, CancellationToken ct = default) =>
        ShowAsync(
            "Focus Session Started",
            $"Stay focused for the next {focusMinutes} minutes. You can do this.",
            NotificationSeverity.Information, ct);

    public Task ShowFocusCompleteAsync(int completedSessions, CancellationToken ct = default) =>
        ShowAsync(
            "Pomodoro Complete!",
            $"Nice work — that's {completedSessions} session(s) today.",
            NotificationSeverity.Success, ct);

    public Task ShowOverActivityAlertAsync(string message, CancellationToken ct = default) =>
        ShowAsync("Rest Harder", message, NotificationSeverity.Warning, ct);
}

/// <summary>
/// Platform-agnostic sink implemented by the App layer using Avalonia's
/// WindowNotificationManager.
/// </summary>
public interface INotificationSink
{
    void Show(string title, string body, NotificationSeverity severity);
}

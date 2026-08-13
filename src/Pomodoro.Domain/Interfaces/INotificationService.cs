using Pomodoro.Domain.Enums;

namespace Pomodoro.Domain.Interfaces;

public interface INotificationService
{
    Task ShowAsync(string title, string body, NotificationSeverity severity = NotificationSeverity.Information, CancellationToken ct = default);

    Task ShowBreakStartAsync(int breakMinutes, bool isLongBreak, CancellationToken ct = default);

    Task ShowFocusStartAsync(int focusMinutes, CancellationToken ct = default);

    Task ShowFocusCompleteAsync(int completedSessions, CancellationToken ct = default);

    Task ShowOverActivityAlertAsync(string message, CancellationToken ct = default);
}

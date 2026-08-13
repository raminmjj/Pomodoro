namespace Pomodoro.Domain.Events;

/// <summary>
/// Raised when an over-activity threshold is exceeded during a break.
/// </summary>
public sealed class ActivityAlertEventArgs : System.EventArgs
{
    public string AlertId { get; }
    public string Title { get; }
    public string Message { get; }
    public int KeyPressesInLastMinute { get; }
    public int MouseClicksInLastMinute { get; }
    public int IdleSecondsInLastMinute { get; }

    public ActivityAlertEventArgs(
        string alertId, string title, string message,
        int keyPressesInLastMinute, int mouseClicksInLastMinute, int idleSecondsInLastMinute)
    {
        AlertId = alertId;
        Title = title;
        Message = message;
        KeyPressesInLastMinute = keyPressesInLastMinute;
        MouseClicksInLastMinute = mouseClicksInLastMinute;
        IdleSecondsInLastMinute = idleSecondsInLastMinute;
    }
}

namespace Pomodoro.Domain.Entities;

/// <summary>
/// One second-snapshot of input activity during a break.
/// Stored to power the daily report and the over-activity alert.
/// </summary>
public sealed class BreakActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The break PomodoroSession this snapshot belongs to.</summary>
    public Guid BreakSessionId { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Number of key presses in this 1-second window.</summary>
    public int KeyPressCount { get; set; }

    /// <summary>Number of mouse button presses in this 1-second window.</summary>
    public int MouseClickCount { get; set; }

    /// <summary>Total mouse movement distance in pixels during this window.</summary>
    public int MouseDistancePx { get; set; }

    /// <summary>Seconds since the last input event (capped at 60 in the tracker).</summary>
    public int IdleSeconds { get; set; }
}

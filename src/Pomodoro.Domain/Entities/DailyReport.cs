namespace Pomodoro.Domain.Entities;

/// <summary>
/// Pre-aggregated daily report row, generated at end-of-day (or on demand).
/// Lets the report screen render quickly without re-scanning every session row.
/// </summary>
public sealed class DailyReport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The UTC date this report covers (date component only).</summary>
    public DateTime Date { get; set; }

    public int CompletedFocusSessions { get; set; }

    public int TotalFocusSeconds { get; set; }

    public int TotalBreakSeconds { get; set; }

    public int TotalKeystrokes { get; set; }

    public int TotalMouseClicks { get; set; }

    public int TotalIdleSeconds { get; set; }

    /// <summary>JSON-serialised list of {taskTitle, minutesSpent} for the breakdown pie.</summary>
    public string TaskBreakdownJson { get; set; } = "[]";

    /// <summary>JSON-serialised list of 24 ints (one per hour) of keystroke counts.</summary>
    public string HourlyKeystrokesJson { get; set; } = "[]";

    /// <summary>JSON-serialised list of 24 ints (one per hour) of mouse-click counts.</summary>
    public string HourlyMouseClicksJson { get; set; } = "[]";

    /// <summary>JSON-serialised list of 24 ints (one per hour) of focus minutes.</summary>
    public string HourlyFocusMinutesJson { get; set; } = "[]";

    /// <summary>JSON-serialised list of 24 ints (one per hour) of break minutes.</summary>
    public string HourlyBreakMinutesJson { get; set; } = "[]";

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

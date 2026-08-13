using Pomodoro.Domain.Enums;

namespace Pomodoro.Application.DTOs;

/// <summary>
/// Aggregated stats for one day, used by the daily-report screen.
/// </summary>
public sealed class DailyReportDto
{
    public DateTime Date { get; set; }
    public TimeSpan TotalFocusTime { get; set; }
    public TimeSpan TotalBreakTime { get; set; }
    public int CompletedSessions { get; set; }
    public int CompletedFocusSessions { get; set; }
    public int TotalKeystrokes { get; set; }
    public int TotalMouseClicks { get; set; }
    public int TotalIdleSeconds { get; set; }

    public List<SessionSegmentDto> Sessions { get; set; } = new();
    public List<TaskBreakdownDto> TaskBreakdown { get; set; } = new();
    public int[] HourlyKeystrokes { get; set; } = new int[24];
    public int[] HourlyMouseClicks { get; set; } = new int[24];

    public TaskBreakdownDto? TopTask => TaskBreakdown
        .OrderByDescending(t => t.MinutesSpent)
        .FirstOrDefault();
}

public sealed class SessionSegmentDto
{
    public Guid Id { get; set; }
    public SessionPhase Phase { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int PlannedDurationSec { get; set; }
    public bool WasCompleted { get; set; }
    public Guid? TaskId { get; set; }
}

public sealed class TaskBreakdownDto
{
    public Guid TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public int MinutesSpent { get; set; }
    public int SessionCount { get; set; }
}

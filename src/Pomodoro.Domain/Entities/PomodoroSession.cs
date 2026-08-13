using Pomodoro.Domain.Enums;

namespace Pomodoro.Domain.Entities;

/// <summary>
/// One Pomodoro cycle (focus phase + its associated break, if any).
/// </summary>
public sealed class PomodoroSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Task this session was logged against, if any.</summary>
    public Guid? TaskId { get; set; }

    public SessionPhase Phase { get; set; } = SessionPhase.Idle;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    /// <summary>Planned duration in seconds.</summary>
    public int PlannedDurationSec { get; set; }

    /// <summary>Actual duration in seconds (may be less if user stopped early).</summary>
    public int ActualDurationSec { get; set; }

    public bool WasCompleted { get; set; }

    /// <summary>If not completed, why. e.g. "user_stop", "user_skip".</summary>
    public string? AbandonReason { get; set; }

    /// <summary>0-based index of this cycle in the day (for long-break logic).</summary>
    public int CycleIndex { get; set; }

    public bool IsLongBreak { get; set; }
}

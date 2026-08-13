namespace Pomodoro.Domain.Enums;

/// <summary>
/// Represents the current phase of a Pomodoro cycle.
/// </summary>
public enum SessionPhase
{
    /// <summary>User has not started a cycle yet, or stopped the previous one.</summary>
    Idle = 0,

    /// <summary>Focus session is running.</summary>
    FocusRunning = 1,

    /// <summary>Focus session is paused.</summary>
    FocusPaused = 2,

    /// <summary>Break session is running (short or long).</summary>
    BreakRunning = 3,

    /// <summary>Break session is paused.</summary>
    BreakPaused = 4,

    /// <summary>A cycle has just completed (transient — engine moves to next phase).</summary>
    Completed = 5,
}

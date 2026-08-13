using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Events;
using Pomodoro.Domain.Enums;

namespace Pomodoro.Domain.Interfaces;

/// <summary>
/// Owns the Pomodoro state machine, drives the per-second tick,
/// and emits state transitions for consumers (UI, activity tracker, notifications).
/// </summary>
public interface IPomodoroEngine : IAsyncDisposable
{
    SessionPhase CurrentPhase { get; }

    TimeSpan Remaining { get; }

    TimeSpan PlannedDuration { get; }

    int CycleIndex { get; }

    event EventHandler<PomodoroStateEventArgs>? StateChanged;

    event EventHandler<TimeSpan>? Tick;

    /// <summary>
    /// Initializes the engine by restoring persisted state (e.g., cycle counter).
    /// Call once at startup before the tick loop begins.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Start a focus session. Optionally attach to a task.</summary>
    Task StartFocusAsync(Guid? taskId = null, CancellationToken ct = default);

    Task PauseAsync(CancellationToken ct = default);

    Task ResumeAsync(CancellationToken ct = default);

    /// <summary>Skip the current phase (counts as completed but with reason=skip).</summary>
    Task SkipCurrentPhaseAsync(CancellationToken ct = default);

    /// <summary>Hard stop the current session without completion credit.</summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>Called by the host's per-second timer. Updates remaining time and emits Tick.</summary>
    Task OnSecondTickAsync(CancellationToken ct = default);

    double GetProgressPercent();
}

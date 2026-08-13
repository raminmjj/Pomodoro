using Pomodoro.Domain.Events;

namespace Pomodoro.Domain.Interfaces;

/// <summary>
/// Evaluates activity snapshots against thresholds and raises
/// <see cref="ActivityAlertEventArgs"/> when over-activity is detected.
/// Implements cooldown logic so the user is not spammed.
/// </summary>
public interface IActivityAlertEvaluator
{
    event EventHandler<ActivityAlertEventArgs>? AlertRaised;

    /// <summary>Push a snapshot for evaluation.</summary>
    Task EvaluateAsync(int keyPresses, int mouseClicks, int idleSeconds, CancellationToken ct = default);

    /// <summary>Reset internal cooldown state (called when a new break starts).</summary>
    void Reset();
}

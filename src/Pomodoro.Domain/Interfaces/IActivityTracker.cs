using Pomodoro.Domain.Entities;

namespace Pomodoro.Domain.Interfaces;

/// <summary>
/// Captures keyboard/mouse activity during break sessions.
/// </summary>
public interface IActivityTracker : IAsyncDisposable
{
    bool IsRunning { get; }

    /// <summary>Begin tracking for the given break session ID.</summary>
    Task StartTrackingAsync(Guid breakSessionId, CancellationToken ct = default);

    Task StopTrackingAsync(CancellationToken ct = default);

    /// <summary>
    /// Called once per second by the engine. Persists a snapshot to the
    /// repository and returns the snapshot for the alert evaluator.
    /// </summary>
    Task<BreakActivity?> TakeSnapshotAsync(CancellationToken ct = default);
}

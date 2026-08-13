using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Infrastructure.Hooks;

/// <summary>
/// No-op tracker used when the user disables activity tracking in settings.
/// </summary>
public sealed class NullActivityTracker : IActivityTracker
{
    private readonly ILogger<NullActivityTracker> _logger;
    public bool IsRunning => false;

    public NullActivityTracker(ILogger<NullActivityTracker> logger) => _logger = logger;

    public Task StartTrackingAsync(Guid breakSessionId, CancellationToken ct = default)
    {
        _logger.LogDebug("Tracking disabled — StartTracking ignored for {BreakId}", breakSessionId);
        return Task.CompletedTask;
    }

    public Task StopTrackingAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<BreakActivity?> TakeSnapshotAsync(CancellationToken ct = default) =>
        Task.FromResult<BreakActivity?>(null);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Events;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Application.Services;

/// <summary>
/// Default alert evaluator: triggers when keystrokes per minute exceed
/// the configured threshold OR idle time is below 5 seconds for the whole minute.
/// Cooldown period prevents spamming the user.
/// </summary>
public sealed class ActivityAlertEvaluator : IActivityAlertEvaluator
{
    private readonly ISettingsService _settings;
    private readonly ILogger<ActivityAlertEvaluator> _logger;

    private readonly object _windowLock = new();
    private readonly Queue<Snapshot> _window = new();  // rolling 60-second window

    private DateTime _lastAlertUtc = DateTime.MinValue;
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    public event EventHandler<ActivityAlertEventArgs>? AlertRaised;

    public ActivityAlertEvaluator(ISettingsService settings, ILogger<ActivityAlertEvaluator> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public void Reset()
    {
        lock (_windowLock)
        {
            _window.Clear();
            _lastAlertUtc = DateTime.MinValue;
        }
    }

    public async Task EvaluateAsync(int keyPresses, int mouseClicks, int idleSeconds, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var snap = new Snapshot(now, keyPresses, mouseClicks, Math.Min(idleSeconds, 60));

        int keysTotal, clicksTotal, idleTotal;
        lock (_windowLock)
        {
            _window.Enqueue(snap);
            while (_window.Count > 0 && (now - _window.Peek().Timestamp).TotalSeconds > 60)
                _window.Dequeue();
            keysTotal = _window.Sum(s => s.KeyPresses);
            clicksTotal = _window.Sum(s => s.MouseClicks);
            idleTotal = _window.Sum(s => s.IdleSeconds);
        }

        // Don't evaluate until we have at least 30 seconds of data
        if (_window.Count < 30) return;

        var threshold = await _settings.GetKeystrokeAlertThresholdAsync(ct);
        var overActivity = keysTotal > threshold || (idleTotal < 5 && (keysTotal + clicksTotal) > 10);

        if (!overActivity) return;

        // Cooldown check
        if ((now - _lastAlertUtc) < Cooldown) return;

        _lastAlertUtc = now;
        var message = keysTotal > threshold
            ? $"You typed {keysTotal} keystrokes in the last minute. Step away from the keyboard to actually rest."
            : "You haven't been idle for more than 5 seconds. Try a real break — close your eyes or stand up.";

        var args = new ActivityAlertEventArgs(
            alertId: $"over-activity-{now:yyyyMMddHHmmss}",
            title: "Rest harder",
            message: message,
            keyPressesInLastMinute: keysTotal,
            mouseClicksInLastMinute: clicksTotal,
            idleSecondsInLastMinute: idleTotal);

        _logger.LogInformation("Activity alert raised: keys={Keys}, clicks={Clicks}, idle={Idle}s",
            keysTotal, clicksTotal, idleTotal);
        AlertRaised?.Invoke(this, args);
    }

    private sealed record Snapshot(DateTime Timestamp, int KeyPresses, int MouseClicks, int IdleSeconds);
}

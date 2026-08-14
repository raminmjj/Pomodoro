using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Interfaces;
using SharpHook;
using SharpHook.Native;

namespace Pomodoro.Infrastructure.Hooks;

/// <summary>
/// Activity tracker backed by SharpHook. Captures global keyboard/mouse
/// events during break sessions. Uses a bounded channel to safely hand
/// work off the native hook thread to a background consumer.
/// </summary>
public sealed class SharpHookActivityTracker : IActivityTracker
{
    private readonly IRepository<BreakActivity> _repo;
    private readonly ILogger<SharpHookActivityTracker> _logger;

    private readonly Channel<BreakActivity> _channel;
    private readonly Task _consumerTask;

    private SimpleGlobalHook? _hook;

    private int _keyPressCount;
    private int _mouseClickCount;
    private int _mouseDistancePx;
    private int _idleSeconds;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private MousePoint _lastMousePos;
    private bool _hasLastMousePos;
    private Guid? _currentBreakId;
    private int _isRunning;  // 0 = stopped, 1 = running

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

    public SharpHookActivityTracker(IRepository<BreakActivity> repo, ILogger<SharpHookActivityTracker> logger)
    {
        _repo = repo;
        _logger = logger;

        _channel = Channel.CreateBounded<BreakActivity>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        _consumerTask = Task.Run(ConsumeLoopAsync);
    }

    public async Task StartTrackingAsync(Guid breakSessionId, CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _isRunning, 1) == 1) return;

        // Check macOS accessibility permission before starting hooks
        if (!MacOsAccessibilityChecker.EnsureAccessibilityPermission(_logger, promptUser: true))
        {
            _logger.LogWarning("Cannot start activity tracking — accessibility permission not granted");
            Interlocked.Exchange(ref _isRunning, 0);
            return;
        }

        _currentBreakId = breakSessionId;
        Interlocked.Exchange(ref _keyPressCount, 0);
        Interlocked.Exchange(ref _mouseClickCount, 0);
        Interlocked.Exchange(ref _mouseDistancePx, 0);
        Interlocked.Exchange(ref _idleSeconds, 0);
        _lastActivityUtc = DateTime.UtcNow;
        _hasLastMousePos = false;

        // Create a fresh hook instance each time (SimpleGlobalHook cannot be reused after Dispose)
        var hook = new SimpleGlobalHook();
        hook.KeyPressed += OnKeyPressed;
        hook.MouseClicked += OnMouseClicked;
        hook.MouseMoved += OnMouseMoved;
        hook.MouseDragged += OnMouseMoved;
        _hook = hook;

        // RunAsync returns a Task that completes when Dispose() is called
        _ = Task.Run(async () =>
        {
            try
            {
                await hook.RunAsync();
            }
            catch (Exception ex) when (ex is not ObjectDisposedException)
            {
                _logger.LogError(ex, "SharpHook run failed");

                var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
                if (!string.IsNullOrEmpty(waylandDisplay))
                {
                    _logger.LogWarning(
                        "Global hooks failed on Wayland ({WaylandDisplay}). " +
                        "SharpHook requires X11/XWayland. Activity tracking may not work. " +
                        "Consider running under an X11 session or enabling XWayland.",
                        waylandDisplay);
                }
            }
        });

        _logger.LogInformation("Activity tracking started for break {BreakId}", breakSessionId);
        await Task.CompletedTask;
    }

    public async Task StopTrackingAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _isRunning, 0) == 0) return;

        // Dispose stops the RunAsync task and releases native resources
        try { _hook?.Dispose(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error disposing SharpHook"); }
        _hook = null;

        _currentBreakId = null;
        _logger.LogInformation("Activity tracking stopped");
        await Task.CompletedTask;
    }

    public async Task<BreakActivity?> TakeSnapshotAsync(CancellationToken ct = default)
    {
        if (!IsRunning || _currentBreakId is null) return null;

        var now = DateTime.UtcNow;
        var idle = (int)(now - _lastActivityUtc).TotalSeconds;
        if (idle > 60) idle = 60;

        // Atomically snapshot and reset counters
        var keys = Interlocked.Exchange(ref _keyPressCount, 0);
        var clicks = Interlocked.Exchange(ref _mouseClickCount, 0);
        var mousePx = Interlocked.Exchange(ref _mouseDistancePx, 0);

        var snapshot = new BreakActivity
        {
            BreakSessionId = _currentBreakId.Value,
            CapturedAt = now,
            KeyPressCount = keys,
            MouseClickCount = clicks,
            MouseDistancePx = mousePx,
            IdleSeconds = idle,
        };

        // Update idle counter for alert evaluator
        Interlocked.Exchange(ref _idleSeconds, idle);

        await _channel.Writer.WriteAsync(snapshot, ct);
        return snapshot;
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        Interlocked.Increment(ref _keyPressCount);
        _lastActivityUtc = DateTime.UtcNow;
    }

    private void OnMouseClicked(object? sender, MouseHookEventArgs e)
    {
        Interlocked.Increment(ref _mouseClickCount);
        _lastActivityUtc = DateTime.UtcNow;
    }

    private void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        var current = new MousePoint((int)e.Data.X, (int)e.Data.Y);
        if (_hasLastMousePos)
        {
            var dx = current.X - _lastMousePos.X;
            var dy = current.Y - _lastMousePos.Y;
            var dist = (int)Math.Sqrt(dx * dx + dy * dy);
            Interlocked.Add(ref _mouseDistancePx, dist);
        }
        _lastMousePos = current;
        _hasLastMousePos = true;
        _lastActivityUtc = DateTime.UtcNow;
    }

    private async Task ConsumeLoopAsync()
    {
        await foreach (var snapshot in _channel.Reader.ReadAllAsync())
        {
            try
            {
                await _repo.UpsertAsync(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist activity snapshot");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopTrackingAsync();

        // Complete the channel so the consumer loop exits
        _channel.Writer.TryComplete();
        try { await _consumerTask.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (TimeoutException) { _logger.LogWarning("Activity consumer task did not exit within timeout"); }
        catch { /* ignore other exceptions */ }
    }

    private readonly record struct MousePoint(int X, int Y);
}

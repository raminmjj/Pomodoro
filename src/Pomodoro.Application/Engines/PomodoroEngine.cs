using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Events;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Application.Engines;

/// <summary>
/// Drives the Pomodoro state machine. Uses a coarse-grained 1-second tick
/// (driven by the host — typically a DispatcherTimer in the App layer)
/// to update remaining time and emit transition events.
///
/// All long-running work is async and cancellation-aware. The engine is
/// a singleton in DI — only one cycle can be running at a time.
/// </summary>
public sealed class PomodoroEngine : IPomodoroEngine
{
    private readonly ISettingsService _settings;
    private readonly IRepository<PomodoroSession> _sessionRepo;
    private readonly INotificationService _notifications;
    private readonly ISoundPlayer _sound;
    private readonly IActivityTracker _activityTracker;
    private readonly ILogger<PomodoroEngine> _logger;

    private readonly object _stateLock = new();

    private PomodoroSession? _currentSession;
    private DateTime _phaseStartTimeUtc;
    private TimeSpan _remaining;
    private TimeSpan _plannedDuration;
    private int _cycleIndex;

    public SessionPhase CurrentPhase { get; private set; } = SessionPhase.Idle;

    public TimeSpan Remaining => _remaining;

    public TimeSpan PlannedDuration => _plannedDuration;

    public int CycleIndex => Volatile.Read(ref _cycleIndex);

    public event EventHandler<PomodoroStateEventArgs>? StateChanged;
    public event EventHandler<TimeSpan>? Tick;

    /// <summary>
    /// Silence gap between repeated break-completion alarm plays while the
    /// engine waits in Idle for the user (Auto-Start next focus unchecked).
    /// </summary>
    public TimeSpan BreakAlarmRepeatGap { get; set; } = TimeSpan.FromSeconds(5);

    private CancellationTokenSource? _alarmRepeatCts;

    /// <summary>
    /// Initializes the engine by restoring cycle counter from the last persisted session.
    /// Should be called once at startup before the tick loop begins.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            var allSessions = await _sessionRepo.GetAllAsync(ct);
            var todayLocal = DateTime.Today;

            // Count completed focus sessions that started today (local date).
            // This resets the cycle counter at the start of each day.
            var todayCompletedFocus = allSessions
                .Count(s => s.Phase == SessionPhase.FocusRunning
                         && s.WasCompleted
                         && s.StartedAt.Date == todayLocal);

            Volatile.Write(ref _cycleIndex, todayCompletedFocus);
            _logger.LogInformation("Restored cycle counter to {CycleIndex} ({Count} completed focus sessions today)",
                todayCompletedFocus, todayCompletedFocus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore cycle counter — starting at 0");
        }
    }

    public PomodoroEngine(
        ISettingsService settings,
        IRepository<PomodoroSession> sessionRepo,
        INotificationService notifications,
        ISoundPlayer sound,
        IActivityTracker activityTracker,
        ILogger<PomodoroEngine> logger)
    {
        _settings = settings;
        _sessionRepo = sessionRepo;
        _notifications = notifications;
        _sound = sound;
        _activityTracker = activityTracker;
        _logger = logger;
    }

    public async Task StartFocusAsync(Guid? taskId = null, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (CurrentPhase != SessionPhase.Idle)
            {
                _logger.LogDebug("StartFocus ignored — phase is {Phase}", CurrentPhase);
                return;
            }
        }

        // The user answered — silence any repeating break-completion alarm.
        StopBreakAlarmRepeat();

        var duration = await _settings.GetFocusDurationAsync(ct);
        var now = DateTime.UtcNow;
        var session = new PomodoroSession
        {
            TaskId = taskId,
            Phase = SessionPhase.FocusRunning,
            StartedAt = now,
            PlannedDurationSec = (int)duration.TotalSeconds,
            CycleIndex = Volatile.Read(ref _cycleIndex),
        };

        await _sessionRepo.UpsertAsync(session, ct);

        lock (_stateLock)
        {
            _currentSession = session;
            _plannedDuration = duration;
            _remaining = duration;
            _phaseStartTimeUtc = now;
            CurrentPhase = SessionPhase.FocusRunning;
        }

        await _notifications.ShowFocusStartAsync((int)duration.TotalMinutes, ct);
        RaiseStateChanged();
        _logger.LogInformation("Focus started: {Minutes} min, task={TaskId}", duration.TotalMinutes, taskId);
    }

    public Task PauseAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (CurrentPhase is not (SessionPhase.FocusRunning or SessionPhase.BreakRunning))
                return Task.CompletedTask;

            CurrentPhase = CurrentPhase == SessionPhase.FocusRunning
                ? SessionPhase.FocusPaused
                : SessionPhase.BreakPaused;
        }

        RaiseStateChanged();
        _logger.LogInformation("Paused at phase {Phase}", CurrentPhase);
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (CurrentPhase is not (SessionPhase.FocusPaused or SessionPhase.BreakPaused))
                return Task.CompletedTask;

            CurrentPhase = CurrentPhase == SessionPhase.FocusPaused
                ? SessionPhase.FocusRunning
                : SessionPhase.BreakRunning;

            _phaseStartTimeUtc = DateTime.UtcNow;
        }

        RaiseStateChanged();
        _logger.LogInformation("Resumed to phase {Phase}", CurrentPhase);
        return Task.CompletedTask;
    }

    public async Task SkipCurrentPhaseAsync(CancellationToken ct = default)
    {
        PomodoroSession? session;
        lock (_stateLock)
        {
            session = _currentSession;
        }

        if (session is null) return;

        session.EndedAt = DateTime.UtcNow;
        session.ActualDurationSec = (int)((DateTime)session.EndedAt - session.StartedAt).TotalSeconds;
        session.WasCompleted = false;
        session.AbandonReason = "user_skip";
        await _sessionRepo.UpsertAsync(session, ct);

        await TransitionAfterPhaseAsync(session, wasCompleted: false, ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        PomodoroSession? session;
        lock (_stateLock)
        {
            session = _currentSession;
            _currentSession = null;
            CurrentPhase = SessionPhase.Idle;
            _remaining = TimeSpan.Zero;
            _plannedDuration = TimeSpan.Zero;
        }

        // Also silences a repeating break-completion alarm, if any.
        StopBreakAlarmRepeat();

        // Stop activity tracking if stopping during a break
        await _activityTracker.StopTrackingAsync(ct);

        if (session is not null)
        {
            session.EndedAt = DateTime.UtcNow;
            session.WasCompleted = false;
            session.AbandonReason = "user_stop";
            session.ActualDurationSec = session.EndedAt is { } ended
                ? (int)(ended - session.StartedAt).TotalSeconds : 0;
            await _sessionRepo.UpsertAsync(session, ct);
        }

        RaiseStateChanged();
        _logger.LogInformation("Stopped. Session persisted: {SessionId}", session?.Id);
    }

    /// <summary>
    /// Called by the host's per-second timer.
    /// </summary>
    public async Task OnSecondTickAsync(CancellationToken ct = default)
    {
        TimeSpan remaining;
        SessionPhase phase;
        PomodoroSession? session;

        lock (_stateLock)
        {
            if (CurrentPhase is not (SessionPhase.FocusRunning or SessionPhase.BreakRunning))
                return;

            var elapsedSinceLastTick = DateTime.UtcNow - _phaseStartTimeUtc;
            _phaseStartTimeUtc = DateTime.UtcNow;
            _remaining = _remaining.Subtract(elapsedSinceLastTick);
            if (_remaining < TimeSpan.Zero) _remaining = TimeSpan.Zero;

            remaining = _remaining;
            phase = CurrentPhase;
            session = _currentSession;
        }

        Tick?.Invoke(this, remaining);

        // Take activity snapshot during breaks
        if (phase == SessionPhase.BreakRunning)
            await _activityTracker.TakeSnapshotAsync(ct);

        if (remaining <= TimeSpan.Zero && session is not null)
            await OnPhaseCompletedAsync(session, ct);
    }

    private async Task OnPhaseCompletedAsync(PomodoroSession session, CancellationToken ct)
    {
        session.EndedAt = DateTime.UtcNow;
        session.WasCompleted = true;
        session.ActualDurationSec = session.PlannedDurationSec;
        await _sessionRepo.UpsertAsync(session, ct);

        // Play alarm + notification (non-fatal — failures must not block
        // the phase transition, otherwise the engine gets stuck).
        try
        {
            var soundName = await _settings.GetAlarmSoundNameAsync(ct);
            var volume = await _settings.GetAlarmVolumeAsync(ct);
            await _sound.PlayAsync(soundName, volume, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to play alarm sound");
        }

        await TransitionAfterPhaseAsync(session, wasCompleted: true, ct);
    }

    private async Task TransitionAfterPhaseAsync(PomodoroSession completed, bool wasCompleted, CancellationToken ct)
    {
        // Focus completed → start break (short or long)
        if (completed.Phase == SessionPhase.FocusRunning && wasCompleted)
        {
            var newCycleIndex = Volatile.Read(ref _cycleIndex) + 1;
            Volatile.Write(ref _cycleIndex, newCycleIndex);

            var sessionsBeforeLong = await _settings.GetSessionsBeforeLongBreakAsync(ct);
            var isLongBreak = newCycleIndex % sessionsBeforeLong == 0;
            var breakDuration = isLongBreak
                ? await _settings.GetLongBreakDurationAsync(ct)
                : await _settings.GetShortBreakDurationAsync(ct);

            await _notifications.ShowFocusCompleteAsync(newCycleIndex, ct);

            var breakSession = new PomodoroSession
            {
                // The break belongs to the task that was being worked on: keeping
                // the TaskId lets auto-start after the break (StartFocusAsync with
                // the completed break session) preserve the task instead of
                // silently logging the next focus session under "(no task)" while
                // the UI still shows the task title.
                TaskId = completed.TaskId,
                Phase = SessionPhase.BreakRunning,
                StartedAt = DateTime.UtcNow,
                PlannedDurationSec = (int)breakDuration.TotalSeconds,
                CycleIndex = newCycleIndex,
                IsLongBreak = isLongBreak,
            };
            await _sessionRepo.UpsertAsync(breakSession, ct);

            lock (_stateLock)
            {
                _currentSession = breakSession;
                _plannedDuration = breakDuration;
                _remaining = breakDuration;
                _phaseStartTimeUtc = DateTime.UtcNow;
                CurrentPhase = SessionPhase.BreakRunning;
            }

            // Start activity tracking during break (non-fatal)
            try { await _activityTracker.StartTrackingAsync(breakSession.Id, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to start activity tracking for break"); }

            await _notifications.ShowBreakStartAsync((int)breakDuration.TotalMinutes, isLongBreak, ct);
            RaiseStateChanged();
            _logger.LogInformation("Break started ({Minutes} min, long={IsLong})", breakDuration.TotalMinutes, isLongBreak);
        }
        // Break completed → back to focus (auto-start if configured) or idle
        else if (completed.Phase == SessionPhase.BreakRunning)
        {
            try { await _activityTracker.StopTrackingAsync(ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to stop activity tracking"); }

            lock (_stateLock)
            {
                _currentSession = null;
                CurrentPhase = SessionPhase.Idle;
                _remaining = TimeSpan.Zero;
                _plannedDuration = TimeSpan.Zero;
            }

            RaiseStateChanged();

            var autoStart = await _settings.GetAutoStartBreakAsync(ct);
            if (autoStart)
            {
                try
                {
                    await StartFocusAsync(completed.TaskId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-start of focus session failed after break completion — engine remains idle");
                    // Nobody would start the next session — keep ringing instead.
                    StartBreakAlarmRepeat();
                }
            }
            else
            {
                // Auto-Start is off: the engine now waits in Idle for the user.
                // Repeat the break-completion alarm (with a gap between plays)
                // until they start the next session.
                StartBreakAlarmRepeat();
            }
        }
        else
        {
            // Skip on focus without completion → back to idle
            lock (_stateLock)
            {
                _currentSession = null;
                CurrentPhase = SessionPhase.Idle;
                _remaining = TimeSpan.Zero;
                _plannedDuration = TimeSpan.Zero;
            }
            RaiseStateChanged();
        }
    }

    public double GetProgressPercent()
    {
        var planned = _plannedDuration.TotalSeconds;
        if (planned <= 0) return 0;
        var elapsed = planned - _remaining.TotalSeconds;
        var pct = elapsed / planned * 100.0;
        return pct < 0 ? 0 : pct > 100 ? 100 : pct;
    }

    private void RaiseStateChanged()
    {
        PomodoroStateEventArgs args;
        lock (_stateLock)
        {
            args = new PomodoroStateEventArgs(CurrentPhase, _currentSession, Volatile.Read(ref _cycleIndex));
        }
        StateChanged?.Invoke(this, args);
    }

    /// <summary>
    /// Repeats the break-completion alarm while the engine waits in Idle for
    /// the user (Auto-Start next focus unchecked). The first playback happens
    /// in OnPhaseCompletedAsync; this loop adds replays separated by
    /// <see cref="BreakAlarmRepeatGap"/> of silence. It stops on the next
    /// StartFocusAsync/StopAsync or on disposal.
    /// </summary>
    private void StartBreakAlarmRepeat()
    {
        CancellationTokenSource cts;
        lock (_stateLock)
        {
            try { _alarmRepeatCts?.Cancel(); }
            catch (ObjectDisposedException) { /* previous loop already exited */ }
            cts = new CancellationTokenSource();
            _alarmRepeatCts = cts;
        }

        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(BreakAlarmRepeatGap, token);
                    }
                    catch (OperationCanceledException) { return; }

                    // Defensive: the engine may have moved on for another reason.
                    if (token.IsCancellationRequested || CurrentPhase != SessionPhase.Idle)
                        return;

                    try
                    {
                        var soundName = await _settings.GetAlarmSoundNameAsync(token);
                        var volume = await _settings.GetAlarmVolumeAsync(token);
                        if (token.IsCancellationRequested) return;
                        await _sound.PlayAsync(soundName, volume, token);
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        // Transient failures must not silence the alarm for good.
                        _logger.LogWarning(ex, "Break-completion alarm replay failed");
                    }
                }
            }
            finally
            {
                cts.Dispose();
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// Stops the repeating break-completion alarm and cuts any replay it is
    /// currently playing. No-op when nothing is repeating (e.g. Auto-Start is
    /// on, or the user already started the next session).
    /// </summary>
    public void StopBreakAlarmRepeat()
    {
        CancellationTokenSource? cts;
        lock (_stateLock)
        {
            cts = _alarmRepeatCts;
            _alarmRepeatCts = null;
        }
        if (cts is null) return;
        try { cts.Cancel(); }
        catch (ObjectDisposedException) { /* loop already exited and disposed it */ }

        // Cancelling the loop's token does not abort every backend mid-play
        // (winmm waits out the duration instead of stopping), so cut the
        // in-flight sound explicitly. StopAsync never throws (the player
        // wraps its backend).
        _ = _sound.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        StopBreakAlarmRepeat();
        await _activityTracker.StopTrackingAsync();
        Tick = null;
        StateChanged = null;
    }
}

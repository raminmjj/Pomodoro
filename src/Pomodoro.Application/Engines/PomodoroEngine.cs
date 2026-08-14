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
    /// Initializes the engine by restoring cycle counter from the last persisted session.
    /// Should be called once at startup before the tick loop begins.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            // Find the most recent session to restore cycle index
            var allSessions = await _sessionRepo.GetAllAsync(ct);
            var latestSession = allSessions
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefault();

            if (latestSession is not null)
            {
                Volatile.Write(ref _cycleIndex, latestSession.CycleIndex);
                _logger.LogInformation("Restored cycle counter to {CycleIndex} from session {SessionId}",
                    latestSession.CycleIndex, latestSession.Id);
            }
            else
            {
                _logger.LogInformation("No previous sessions found — starting with cycle counter at 0");
            }
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

        // Play alarm + notification
        var soundName = await _settings.GetAlarmSoundNameAsync(ct);
        var volume = await _settings.GetAlarmVolumeAsync(ct);
        await _sound.PlayAsync(soundName, volume, ct);

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
                TaskId = null,
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

            // Start activity tracking during break
            await _activityTracker.StartTrackingAsync(breakSession.Id, ct);

            await _notifications.ShowBreakStartAsync((int)breakDuration.TotalMinutes, isLongBreak, ct);
            RaiseStateChanged();
            _logger.LogInformation("Break started ({Minutes} min, long={IsLong})", breakDuration.TotalMinutes, isLongBreak);
        }
        // Break completed → back to focus (auto-start if configured) or idle
        else if (completed.Phase == SessionPhase.BreakRunning)
        {
            await _activityTracker.StopTrackingAsync(ct);

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
                await StartFocusAsync(completed.TaskId, ct);
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

    public async ValueTask DisposeAsync()
    {
        await _activityTracker.StopTrackingAsync();
        Tick = null;
        StateChanged = null;
    }
}

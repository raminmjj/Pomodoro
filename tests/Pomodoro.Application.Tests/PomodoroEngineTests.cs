using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pomodoro.Application.Engines;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Interfaces;
using Xunit;

namespace Pomodoro.Application.Tests;

public class PomodoroEngineTests
{
    private readonly IRepository<PomodoroSession> _sessionRepo;
    private readonly ISettingsService _settings;
    private readonly INotificationService _notifications;
    private readonly ISoundPlayer _sound;
    private readonly PomodoroEngine _engine;

    public PomodoroEngineTests()
    {
        _sessionRepo = Substitute.For<IRepository<PomodoroSession>>();
        _settings = Substitute.For<ISettingsService>();
        _notifications = Substitute.For<INotificationService>();
        _sound = Substitute.For<ISoundPlayer>();

        _settings.GetFocusDurationAsync(Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromMinutes(25));
        _settings.GetShortBreakDurationAsync(Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromMinutes(5));
        _settings.GetLongBreakDurationAsync(Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromMinutes(15));
        _settings.GetSessionsBeforeLongBreakAsync(Arg.Any<CancellationToken>())
            .Returns(4);
        _settings.GetAutoStartBreakAsync(Arg.Any<CancellationToken>())
            .Returns(false);
        _settings.GetAlarmSoundNameAsync(Arg.Any<CancellationToken>())
            .Returns("bell");
        _settings.GetAlarmVolumeAsync(Arg.Any<CancellationToken>())
            .Returns(1.0f);

        _engine = new PomodoroEngine(_settings, _sessionRepo, _notifications, _sound, NullLogger<PomodoroEngine>.Instance);
    }

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StartFocus_FromIdle_TransitionsToFocusRunning()
    {
        _engine.CurrentPhase.Should().Be(SessionPhase.Idle);

        await _engine.StartFocusAsync(ct: CT);

        _engine.CurrentPhase.Should().Be(SessionPhase.FocusRunning);
        _engine.Remaining.Should().Be(TimeSpan.FromMinutes(25));
        await _sessionRepo.Received(1).UpsertAsync(
            Arg.Is<PomodoroSession>(s => s.Phase == SessionPhase.FocusRunning),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartFocus_FromFocusRunning_DoesNothing()
    {
        await _engine.StartFocusAsync(ct: CT);
        await _engine.StartFocusAsync(ct: CT);

        // Only the first call should have persisted
        await _sessionRepo.Received(1).UpsertAsync(
            Arg.Any<PomodoroSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pause_FromFocusRunning_TransitionsToFocusPaused()
    {
        await _engine.StartFocusAsync(ct: CT);
        await _engine.PauseAsync(ct: CT);
        _engine.CurrentPhase.Should().Be(SessionPhase.FocusPaused);
    }

    [Fact]
    public async Task Pause_FromIdle_DoesNothing()
    {
        await _engine.PauseAsync(ct: CT);
        _engine.CurrentPhase.Should().Be(SessionPhase.Idle);
    }

    [Fact]
    public async Task Resume_FromFocusPaused_TransitionsToFocusRunning()
    {
        await _engine.StartFocusAsync(ct: CT);
        await _engine.PauseAsync(ct: CT);
        await _engine.ResumeAsync(ct: CT);
        _engine.CurrentPhase.Should().Be(SessionPhase.FocusRunning);
    }

    [Fact]
    public async Task Stop_PersistsAbandonedSession_WithReason()
    {
        await _engine.StartFocusAsync(ct: CT);
        await _engine.StopAsync(ct: CT);

        await _sessionRepo.Received(2).UpsertAsync(
            Arg.Is<PomodoroSession>(s =>
                s.WasCompleted == false && s.AbandonReason == "user_stop"),
            Arg.Any<CancellationToken>());

        _engine.CurrentPhase.Should().Be(SessionPhase.Idle);
    }

    [Fact]
    public async Task GetProgressPercent_AtStart_ReturnsZero()
    {
        await _engine.StartFocusAsync(ct: CT);
        _engine.GetProgressPercent().Should().Be(0);
    }

    [Fact]
    public async Task OnSecondTick_ReducesRemaining_AndRaisesTickEvent()
    {
        await _engine.StartFocusAsync(ct: CT);

        TimeSpan? captured = null;
        _engine.Tick += (_, remaining) => captured = remaining;

        // Manually advance the engine's internal phase start to 5 seconds ago
        await _engine.OnSecondTickAsync(CT);

        captured.Should().NotBeNull();
        // Should be < 25 min after one tick
        captured!.Value.Should().BeLessThan(TimeSpan.FromMinutes(25));
    }

    [Fact]
    public async Task StateChanged_EventIsRaised_OnStart()
    {
        SessionPhase? captured = null;
        _engine.StateChanged += (_, e) => captured = e.Phase;

        await _engine.StartFocusAsync(ct: CT);

        captured.Should().Be(SessionPhase.FocusRunning);
    }
}

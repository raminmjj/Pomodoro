using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using NSubstitute;
using Pomodoro.App.Services;
using Pomodoro.App.ViewModels;
using Pomodoro.App.Views;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Events;
using Pomodoro.Domain.Interfaces;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Pomodoro.App.Tests.E2E.TestApp))]

namespace Pomodoro.App.Tests.E2E;

/// <summary>
/// Minimal Avalonia Application used only for headless test infrastructure.
/// </summary>
public class TestApp : Avalonia.Application
{
    public override void Initialize()
    {
        // No XAML to load — styles are not needed for logic-level E2E tests.
    }
}

/// <summary>
/// End-to-end tests using Avalonia.Headless to verify UI rendering,
/// initial state, user interactions, navigation, and settings persistence.
/// Uses plain [Fact] with assembly-level [AvaloniaTestApplication] for xUnit v3 compatibility.
/// </summary>
public class MainViewTests
{
    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────

    private static IPomodoroEngine CreateIdleEngine()
    {
        var engine = Substitute.For<IPomodoroEngine>();
        engine.CurrentPhase.Returns(SessionPhase.Idle);
        engine.Remaining.Returns(TimeSpan.FromMinutes(25));
        engine.PlannedDuration.Returns(TimeSpan.FromMinutes(25));
        engine.CycleIndex.Returns(0);
        engine.GetProgressPercent().Returns(0d);
        return engine;
    }

    private static INavigationService CreateNavigationService()
    {
        return new NavigationService();
    }

    private static ITaskService CreateTaskService()
    {
        var svc = Substitute.For<ITaskService>();
        svc.IncrementPomodoroCountAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        return svc;
    }

    private static ISettingsService CreateSettingsService()
    {
        var svc = Substitute.For<ISettingsService>();
        svc.GetFocusDurationAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(TimeSpan.FromMinutes(25)));
        svc.GetShortBreakDurationAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(TimeSpan.FromMinutes(5)));
        svc.GetLongBreakDurationAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(TimeSpan.FromMinutes(15)));
        svc.GetSessionsBeforeLongBreakAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(4));
        svc.GetAutoStartBreakAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(false));
        svc.GetActivityTrackingEnabledAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(true));
        svc.GetKeystrokeAlertThresholdAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(60));
        svc.GetAlarmVolumeAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(1.0f));
        svc.GetAlarmSoundNameAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult("bell"));

        svc.SetFocusDurationAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        svc.SetShortBreakDurationAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        svc.SetLongBreakDurationAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        svc.SetSessionsBeforeLongBreakAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        svc.SetAutoStartBreakAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        svc.SetActivityTrackingEnabledAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        svc.SetKeystrokeAlertThresholdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        svc.SetAlarmVolumeAsync(Arg.Any<float>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        svc.SetAlarmSoundNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);

        return svc;
    }

    private static IAutoStartService CreateAutoStartService()
    {
        var svc = Substitute.For<IAutoStartService>();
        svc.IsEnabledAsync(Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(false));
        svc.EnableAsync(Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        svc.DisableAsync(Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        return svc;
    }

    private static ISoundPlayer CreateSoundPlayer()
    {
        var svc = Substitute.For<ISoundPlayer>();
        svc.PlayAsync(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        svc.StopAsync(Arg.Any<CancellationToken>())
           .Returns(Task.CompletedTask);
        return svc;
    }

    private static MainViewModel CreateMainViewModel(
        IPomodoroEngine? engine = null,
        INavigationService? nav = null,
        ITaskService? tasks = null,
        ISettingsService? settings = null)
    {
        return new MainViewModel(
            engine ?? CreateIdleEngine(),
            nav ?? CreateNavigationService(),
            tasks ?? CreateTaskService(),
            settings ?? CreateSettingsService());
    }

    // ──────────────────────────────────────────────
    //  1. Verify MainView renders with correct initial state
    // ──────────────────────────────────────────────

    [Fact]
    public void MainView_Initial_State_Shows_Idle_Phase_And_Default_Timer()
    {
        var vm = CreateMainViewModel();
        var view = new MainView { DataContext = vm };

        view.Measure(new Size(800, 600));
        view.Arrange(new Rect(0, 0, 800, 600));

        Assert.Equal(SessionPhase.Idle, vm.CurrentPhase);
        Assert.Equal("Ready to focus", vm.PhaseLabel);
        Assert.Equal("25:00", vm.TimeRemaining);
        Assert.Equal(0, vm.ProgressPercent);
        Assert.Equal("(no task)", vm.CurrentTaskTitle);
        Assert.True(vm.CanStart);
        Assert.False(vm.CanPause);
        Assert.False(vm.CanResume);
        Assert.False(vm.CanStop);
    }

    // ──────────────────────────────────────────────
    //  2. Verify Start button triggers engine
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Start_Button_Should_Call_Engine_StartFocusAsync()
    {
        var engine = CreateIdleEngine();
        engine.StartFocusAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
              .Returns(Task.CompletedTask);

        var vm = CreateMainViewModel(engine: engine);
        var view = new MainView { DataContext = vm };
        view.Measure(new Size(800, 600));
        view.Arrange(new Rect(0, 0, 800, 600));

        Assert.True(vm.StartCommand.CanExecute(null));
        await vm.StartCommand.ExecuteAsync(null);

        await engine.Received(1).StartFocusAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────
    //  3. Verify state transitions update UI correctly
    // ──────────────────────────────────────────────

    [Fact]
    public void Engine_StateChanged_Should_Update_PhaseLabel_And_ButtonStates()
    {
        var engine = CreateIdleEngine();
        var vm = CreateMainViewModel(engine: engine);
        var view = new MainView { DataContext = vm };
        view.Measure(new Size(800, 600));
        view.Arrange(new Rect(0, 0, 800, 600));

        // Simulate engine raising StateChanged for FocusRunning
        engine.StateChanged += Raise.EventWith(
            new PomodoroStateEventArgs(SessionPhase.FocusRunning, null, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(SessionPhase.FocusRunning, vm.CurrentPhase);
        Assert.Equal("Focusing…", vm.PhaseLabel);
        Assert.False(vm.CanStart);
        Assert.True(vm.CanPause);
        Assert.False(vm.CanResume);
        Assert.True(vm.CanStop);
    }

    // ──────────────────────────────────────────────
    //  4. Verify Pause/Resume toggle
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Pause_And_Resume_Should_Toggle_CanExecute_States()
    {
        var engine = CreateIdleEngine();
        engine.PauseAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        engine.ResumeAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var vm = CreateMainViewModel(engine: engine);
        var view = new MainView { DataContext = vm };
        view.Measure(new Size(800, 600));
        view.Arrange(new Rect(0, 0, 800, 600));

        // Transition to FocusRunning
        engine.StateChanged += Raise.EventWith(
            new PomodoroStateEventArgs(SessionPhase.FocusRunning, null, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.CanPause);
        Assert.False(vm.CanResume);

        // Pause
        await vm.PauseCommand.ExecuteAsync(null);
        engine.StateChanged += Raise.EventWith(
            new PomodoroStateEventArgs(SessionPhase.FocusPaused, null, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.CanPause);
        Assert.True(vm.CanResume);
        Assert.Equal("Focus paused", vm.PhaseLabel);

        // Resume
        await vm.ResumeCommand.ExecuteAsync(null);
        await engine.Received(1).ResumeAsync(Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────
    //  5. Verify navigation between views
    // ──────────────────────────────────────────────

    [Fact]
    public void Navigation_Commands_Should_Raise_ViewChanged_Event()
    {
        var nav = CreateNavigationService();
        AppView? navigatedTo = null;
        nav.ViewChanged += v => navigatedTo = v;

        var vm = CreateMainViewModel(nav: nav);

        vm.GoToTasksCommand.Execute(null);
        Assert.Equal(AppView.TaskList, navigatedTo);

        vm.GoToSettingsCommand.Execute(null);
        Assert.Equal(AppView.Settings, navigatedTo);

        vm.GoToReportCommand.Execute(null);
        Assert.Equal(AppView.DailyReport, navigatedTo);

        vm.GoToMainCommand.Execute(null);
        Assert.Equal(AppView.Main, navigatedTo);
    }

    // ──────────────────────────────────────────────
    //  6. Verify Settings save round-trip
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Settings_Save_Should_Persist_All_Values_To_Service()
    {
        var settings = CreateSettingsService();
        var autostart = CreateAutoStartService();
        var vm = new SettingsViewModel(settings, autostart, CreateSoundPlayer());

        // Wait for LoadAsync (called in constructor)
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        // Modify values and save
        vm.FocusMinutes = 30;
        vm.ShortBreakMinutes = 10;
        vm.LongBreakMinutes = 20;
        vm.SessionsBeforeLong = 3;
        vm.AutoStartBreak = true;
        vm.AlarmSoundName = "chime";
        vm.AlarmVolume = 0.7;

        await vm.SaveCommand.ExecuteAsync(null);

        await settings.Received(1).SetFocusDurationAsync(
            TimeSpan.FromMinutes(30), Arg.Any<CancellationToken>());
        await settings.Received(1).SetShortBreakDurationAsync(
            TimeSpan.FromMinutes(10), Arg.Any<CancellationToken>());
        await settings.Received(1).SetLongBreakDurationAsync(
            TimeSpan.FromMinutes(20), Arg.Any<CancellationToken>());
        await settings.Received(1).SetSessionsBeforeLongBreakAsync(
            3, Arg.Any<CancellationToken>());
        await settings.Received(1).SetAutoStartBreakAsync(
            true, Arg.Any<CancellationToken>());
        await settings.Received(1).SetAlarmSoundNameAsync(
            "chime", Arg.Any<CancellationToken>());
        await settings.Received(1).SetAlarmVolumeAsync(
            Arg.Is<float>(v => Math.Abs(v - 0.7f) < 0.01f), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────
    //  7. Verify Settings load populates ViewModel
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Settings_Load_Should_Populate_From_Service()
    {
        var settings = CreateSettingsService();
        var autostart = CreateAutoStartService();

        var vm = new SettingsViewModel(settings, autostart, CreateSoundPlayer());

        await Task.Delay(100, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(25, vm.FocusMinutes);
        Assert.Equal(5, vm.ShortBreakMinutes);
        Assert.Equal(15, vm.LongBreakMinutes);
        Assert.Equal(4, vm.SessionsBeforeLong);
        Assert.False(vm.AutoStartBreak);
        Assert.True(vm.ActivityTrackingEnabled);
        Assert.Equal(60, vm.KeystrokeAlertThreshold);
        Assert.Equal("bell", vm.AlarmSoundName);
    }

    // ──────────────────────────────────────────────
    //  8. Verify Stop resets to Idle
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Stop_Should_Reset_To_Idle_And_Disable_Controls()
    {
        var engine = CreateIdleEngine();
        engine.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var vm = CreateMainViewModel(engine: engine);
        var view = new MainView { DataContext = vm };
        view.Measure(new Size(800, 600));
        view.Arrange(new Rect(0, 0, 800, 600));

        // Transition to FocusRunning first
        engine.StateChanged += Raise.EventWith(
            new PomodoroStateEventArgs(SessionPhase.FocusRunning, null, 1));
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.CanStop);

        // Stop
        await vm.StopCommand.ExecuteAsync(null);
        engine.StateChanged += Raise.EventWith(
            new PomodoroStateEventArgs(SessionPhase.Idle, null, 0));
        Dispatcher.UIThread.RunJobs();

        await engine.Received(1).StopAsync(Arg.Any<CancellationToken>());
        Assert.Equal(SessionPhase.Idle, vm.CurrentPhase);
        Assert.Equal("Ready to focus", vm.PhaseLabel);
        Assert.True(vm.CanStart);
        Assert.False(vm.CanPause);
        Assert.False(vm.CanStop);
    }
}

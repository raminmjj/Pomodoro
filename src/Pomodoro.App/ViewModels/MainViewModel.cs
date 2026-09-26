using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pomodoro.App.Services;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Events;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.App.ViewModels;

public sealed partial class MainViewModel : BaseViewModel, IDisposable
{
    private readonly IPomodoroEngine _engine;
    private readonly INavigationService _navigation;
    private readonly ITaskService _taskService;
    private readonly ISettingsService _settings;

    public MainViewModel(
        IPomodoroEngine engine,
        INavigationService navigation,
        ITaskService taskService,
        ISettingsService settings,
        ILogger<MainViewModel>? logger = null)
        : base(logger)
    {
        _engine = engine;
        _navigation = navigation;
        _taskService = taskService;
        _settings = settings;
        _engine.StateChanged += OnStateChanged;
        _engine.Tick += OnTick;
        _navigation.ViewChanged += OnViewChanged;
        FireAndForget(LoadInitialTimerAsync);
        UpdateCanExecute();
    }

    private void OnViewChanged(AppView view)
    {
        if (view == AppView.Main && CurrentPhase == SessionPhase.Idle)
            FireAndForget(LoadInitialTimerAsync);
    }

    private async Task LoadInitialTimerAsync()
    {
        var focusDuration = await _settings.GetFocusDurationAsync();
        TimeRemaining = $"{(int)focusDuration.TotalMinutes:D2}:00";
    }

    [ObservableProperty] private string _currentTaskTitle = "(no task)";
    [ObservableProperty] private string _timeRemaining = "00:00";
    [ObservableProperty] private SessionPhase _currentPhase = SessionPhase.Idle;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private bool _canStart = true;
    [ObservableProperty] private bool _canPause;
    [ObservableProperty] private bool _canResume;
    [ObservableProperty] private bool _canStop;
    [ObservableProperty] private int _cycleIndex;
    [ObservableProperty] private string _phaseLabel = "Ready to focus";

    private Guid? _activeTaskId;

    /// <summary>True while a task is selected for the next focus session.</summary>
    public bool IsActiveTaskSet => _activeTaskId.HasValue;

    public void SetActiveTask(Guid? taskId, string? title)
    {
        _activeTaskId = taskId;
        CurrentTaskTitle = string.IsNullOrEmpty(title) ? "(no task)" : title;
        OnPropertyChanged(nameof(IsActiveTaskSet));
    }

    /// <summary>
    /// Clears the active task so the next Start runs "(no task)". Without this,
    /// a previously selected task silently carries over to later sessions and
    /// misattributes their minutes in the reports.
    /// </summary>
    [RelayCommand]
    private void ClearActiveTask() => SetActiveTask(null, null);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        await RunSafeAsync(async () =>
        {
            await _engine.StartFocusAsync(_activeTaskId);
        });
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync()
    {
        await RunSafeAsync(() => _engine.PauseAsync());
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync()
    {
        await RunSafeAsync(() => _engine.ResumeAsync());
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        await RunSafeAsync(() => _engine.StopAsync());
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task SkipAsync()
    {
        await RunSafeAsync(() => _engine.SkipCurrentPhaseAsync());
    }

    [RelayCommand]
    private void GoToMain() => _navigation.NavigateTo(AppView.Main);

    [RelayCommand]
    private void GoToTasks() => _navigation.NavigateTo(AppView.TaskList);

    [RelayCommand]
    private void GoToSettings() => _navigation.NavigateTo(AppView.Settings);

    [RelayCommand]
    private void GoToReport() => _navigation.NavigateTo(AppView.DailyReport);

    private void OnStateChanged(object? sender, PomodoroStateEventArgs e)
    {
        // Focus completed → break started: increment task pomodoro count
        if (e.Phase == SessionPhase.BreakRunning && _activeTaskId.HasValue)
        {
            var taskId = _activeTaskId.Value;
            FireAndForget(async () =>
            {
                try
                {
                    await _taskService.IncrementPomodoroCountAsync(taskId, Guid.Empty);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "Failed to increment pomodoro count for task {TaskId}", taskId);
                }
            });
        }

        Dispatcher.UIThread.Post(() =>
        {
            CurrentPhase = e.Phase;
            CycleIndex = e.CycleIndex;
            PhaseLabel = e.Phase switch
            {
                SessionPhase.Idle => "Ready to focus",
                SessionPhase.FocusRunning => "Focusing…",
                SessionPhase.FocusPaused => "Focus paused",
                SessionPhase.BreakRunning => "On break",
                SessionPhase.BreakPaused => "Break paused",
                SessionPhase.Completed => "Cycle complete",
                _ => "—",
            };
            UpdateCanExecute();
        });
    }

    private void OnTick(object? sender, TimeSpan remaining)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TimeRemaining = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
                : $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            ProgressPercent = _engine.GetProgressPercent();
        });
    }

    private void UpdateCanExecute()
    {
        CanStart = CurrentPhase == SessionPhase.Idle;
        CanPause = CurrentPhase is SessionPhase.FocusRunning or SessionPhase.BreakRunning;
        CanResume = CurrentPhase is SessionPhase.FocusPaused or SessionPhase.BreakPaused;
        CanStop = CurrentPhase != SessionPhase.Idle;

        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        SkipCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _engine.StateChanged -= OnStateChanged;
        _engine.Tick -= OnTick;
    }
}

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pomodoro.App.Services;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.App.ViewModels;

public sealed partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settings;
    private readonly IAutoStartService _autostart;
    private readonly ISoundPlayer _soundPlayer;

    [ObservableProperty] private int _focusMinutes = 25;
    [ObservableProperty] private int _shortBreakMinutes = 5;
    [ObservableProperty] private int _longBreakMinutes = 15;
    [ObservableProperty] private int _sessionsBeforeLong = 4;
    [ObservableProperty] private bool _autoStartBreak;
    [ObservableProperty] private bool _activityTrackingEnabled = true;
    [ObservableProperty] private int _keystrokeAlertThreshold = 60;
    [ObservableProperty] private double _alarmVolume = 1.0;
    [ObservableProperty] private bool _autoStartWithSystem;
    [ObservableProperty] private string _alarmSoundName = "bell";

    public string[] AvailableSounds { get; } = { "bell", "chime", "digital" };

    public SettingsViewModel(ISettingsService settings, IAutoStartService autostart, ISoundPlayer soundPlayer)
    {
        _settings = settings;
        _autostart = autostart;
        _soundPlayer = soundPlayer;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunSafeAsync(async () =>
        {
            FocusMinutes = (int)(await _settings.GetFocusDurationAsync()).TotalMinutes;
            ShortBreakMinutes = (int)(await _settings.GetShortBreakDurationAsync()).TotalMinutes;
            LongBreakMinutes = (int)(await _settings.GetLongBreakDurationAsync()).TotalMinutes;
            SessionsBeforeLong = await _settings.GetSessionsBeforeLongBreakAsync();
            AutoStartBreak = await _settings.GetAutoStartBreakAsync();
            ActivityTrackingEnabled = await _settings.GetActivityTrackingEnabledAsync();
            KeystrokeAlertThreshold = await _settings.GetKeystrokeAlertThresholdAsync();
            AlarmVolume = await _settings.GetAlarmVolumeAsync();
            AlarmSoundName = await _settings.GetAlarmSoundNameAsync();
            AutoStartWithSystem = await _autostart.IsEnabledAsync();
        });
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        await RunSafeAsync(async () =>
        {
            await _settings.SetFocusDurationAsync(TimeSpan.FromMinutes(FocusMinutes));
            await _settings.SetShortBreakDurationAsync(TimeSpan.FromMinutes(ShortBreakMinutes));
            await _settings.SetLongBreakDurationAsync(TimeSpan.FromMinutes(LongBreakMinutes));
            await _settings.SetSessionsBeforeLongBreakAsync(SessionsBeforeLong);
            await _settings.SetAutoStartBreakAsync(AutoStartBreak);
            await _settings.SetActivityTrackingEnabledAsync(ActivityTrackingEnabled);
            await _settings.SetKeystrokeAlertThresholdAsync(KeystrokeAlertThreshold);
            await _settings.SetAlarmVolumeAsync((float)AlarmVolume);
            await _settings.SetAlarmSoundNameAsync(AlarmSoundName);

            // Apply autostart change
            var currentEnabled = await _autostart.IsEnabledAsync();
            if (AutoStartWithSystem && !currentEnabled) await _autostart.EnableAsync();
            if (!AutoStartWithSystem && currentEnabled) await _autostart.DisableAsync();
        });
    }

    [RelayCommand]
    private void Back() => ServiceLocator.GetRequiredService<INavigationService>().NavigateTo(AppView.Main);

    [RelayCommand]
    private async Task PreviewAlarmAsync()
    {
        await RunSafeAsync(async () =>
        {
            await _soundPlayer.PlayAsync(AlarmSoundName, (float)AlarmVolume);
        });
    }
}

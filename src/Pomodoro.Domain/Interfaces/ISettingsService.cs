namespace Pomodoro.Domain.Interfaces;

public interface ISettingsService
{
    Task<TimeSpan> GetFocusDurationAsync(CancellationToken ct = default);
    Task SetFocusDurationAsync(TimeSpan value, CancellationToken ct = default);

    Task<TimeSpan> GetShortBreakDurationAsync(CancellationToken ct = default);
    Task SetShortBreakDurationAsync(TimeSpan value, CancellationToken ct = default);

    Task<TimeSpan> GetLongBreakDurationAsync(CancellationToken ct = default);
    Task SetLongBreakDurationAsync(TimeSpan value, CancellationToken ct = default);

    Task<int> GetSessionsBeforeLongBreakAsync(CancellationToken ct = default);
    Task SetSessionsBeforeLongBreakAsync(int value, CancellationToken ct = default);

    Task<bool> GetAutoStartAsync(CancellationToken ct = default);
    Task SetAutoStartAsync(bool value, CancellationToken ct = default);

    Task<bool> GetAutoStartBreakAsync(CancellationToken ct = default);
    Task SetAutoStartBreakAsync(bool value, CancellationToken ct = default);

    Task<bool> GetActivityTrackingEnabledAsync(CancellationToken ct = default);
    Task SetActivityTrackingEnabledAsync(bool value, CancellationToken ct = default);

    Task<int> GetKeystrokeAlertThresholdAsync(CancellationToken ct = default);
    Task SetKeystrokeAlertThresholdAsync(int value, CancellationToken ct = default);

    Task<float> GetAlarmVolumeAsync(CancellationToken ct = default);
    Task SetAlarmVolumeAsync(float value, CancellationToken ct = default);

    Task<string> GetAlarmSoundNameAsync(CancellationToken ct = default);
    Task SetAlarmSoundNameAsync(string value, CancellationToken ct = default);
}

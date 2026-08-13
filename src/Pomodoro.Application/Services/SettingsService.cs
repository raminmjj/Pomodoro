using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Application.Services;

/// <summary>
/// Persists user preferences as rows in the Settings collection.
/// Defaults are applied on first read if the row is missing.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IRepository<Setting> _repo;
    private readonly ILogger<SettingsService> _logger;

    private static readonly TimeSpan DefaultFocus = TimeSpan.FromMinutes(25);
    private static readonly TimeSpan DefaultShortBreak = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultLongBreak = TimeSpan.FromMinutes(15);
    private const int DefaultSessionsBeforeLong = 4;
    private const int DefaultKeystrokeThreshold = 60;

    public SettingsService(IRepository<Setting> repo, ILogger<SettingsService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public Task<TimeSpan> GetFocusDurationAsync(CancellationToken ct = default) =>
        GetTimeSpanAsync("focus_duration", DefaultFocus, ct);

    public Task SetFocusDurationAsync(TimeSpan value, CancellationToken ct = default) =>
        SetTimeSpanAsync("focus_duration", value, ct);

    public Task<TimeSpan> GetShortBreakDurationAsync(CancellationToken ct = default) =>
        GetTimeSpanAsync("short_break_duration", DefaultShortBreak, ct);

    public Task SetShortBreakDurationAsync(TimeSpan value, CancellationToken ct = default) =>
        SetTimeSpanAsync("short_break_duration", value, ct);

    public Task<TimeSpan> GetLongBreakDurationAsync(CancellationToken ct = default) =>
        GetTimeSpanAsync("long_break_duration", DefaultLongBreak, ct);

    public Task SetLongBreakDurationAsync(TimeSpan value, CancellationToken ct = default) =>
        SetTimeSpanAsync("long_break_duration", value, ct);

    public Task<int> GetSessionsBeforeLongBreakAsync(CancellationToken ct = default) =>
        GetIntAsync("sessions_before_long", DefaultSessionsBeforeLong, ct);

    public Task SetSessionsBeforeLongBreakAsync(int value, CancellationToken ct = default) =>
        SetIntAsync("sessions_before_long", value, ct);

    public Task<bool> GetAutoStartAsync(CancellationToken ct = default) =>
        GetBoolAsync("autostart", false, ct);

    public Task SetAutoStartAsync(bool value, CancellationToken ct = default) =>
        SetBoolAsync("autostart", value, ct);

    public Task<bool> GetAutoStartBreakAsync(CancellationToken ct = default) =>
        GetBoolAsync("autostart_break", true, ct);

    public Task SetAutoStartBreakAsync(bool value, CancellationToken ct = default) =>
        SetBoolAsync("autostart_break", value, ct);

    public Task<bool> GetActivityTrackingEnabledAsync(CancellationToken ct = default) =>
        GetBoolAsync("activity_tracking_enabled", true, ct);

    public Task SetActivityTrackingEnabledAsync(bool value, CancellationToken ct = default) =>
        SetBoolAsync("activity_tracking_enabled", value, ct);

    public Task<int> GetKeystrokeAlertThresholdAsync(CancellationToken ct = default) =>
        GetIntAsync("keystroke_alert_threshold", DefaultKeystrokeThreshold, ct);

    public Task SetKeystrokeAlertThresholdAsync(int value, CancellationToken ct = default) =>
        SetIntAsync("keystroke_alert_threshold", value, ct);

    public Task<float> GetAlarmVolumeAsync(CancellationToken ct = default) =>
        GetFloatAsync("alarm_volume", 1.0f, ct);

    public Task SetAlarmVolumeAsync(float value, CancellationToken ct = default) =>
        SetFloatAsync("alarm_volume", value, ct);

    public Task<string> GetAlarmSoundNameAsync(CancellationToken ct = default) =>
        GetStringAsync("alarm_sound", "bell", ct);

    public Task SetAlarmSoundNameAsync(string value, CancellationToken ct = default) =>
        SetStringAsync("alarm_sound", value, ct);

    // ---- helpers ----

    private async Task<TimeSpan> GetTimeSpanAsync(string key, TimeSpan defaultValue, CancellationToken ct)
    {
        var raw = await GetRawAsync(key, ct);
        if (string.IsNullOrEmpty(raw)) return defaultValue;
        return TimeSpan.TryParse(raw, null, out var ts) ? ts : defaultValue;
    }

    private async Task SetTimeSpanAsync(string key, TimeSpan value, CancellationToken ct) =>
        await SetRawAsync(key, value.ToString(), ct);

    private async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct)
    {
        var raw = await GetRawAsync(key, ct);
        if (string.IsNullOrEmpty(raw)) return defaultValue;
        return int.TryParse(raw, out var v) ? v : defaultValue;
    }

    private async Task SetIntAsync(string key, int value, CancellationToken ct) =>
        await SetRawAsync(key, value.ToString(), ct);

    private async Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct)
    {
        var raw = await GetRawAsync(key, ct);
        if (string.IsNullOrEmpty(raw)) return defaultValue;
        return bool.TryParse(raw, out var v) ? v : defaultValue;
    }

    private async Task SetBoolAsync(string key, bool value, CancellationToken ct) =>
        await SetRawAsync(key, value.ToString(), ct);

    private async Task<float> GetFloatAsync(string key, float defaultValue, CancellationToken ct)
    {
        var raw = await GetRawAsync(key, ct);
        if (string.IsNullOrEmpty(raw)) return defaultValue;
        return float.TryParse(raw, out var v) ? v : defaultValue;
    }

    private async Task SetFloatAsync(string key, float value, CancellationToken ct) =>
        await SetRawAsync(key, value.ToString("0.00"), ct);

    private async Task<string> GetStringAsync(string key, string defaultValue, CancellationToken ct)
    {
        var raw = await GetRawAsync(key, ct);
        return string.IsNullOrEmpty(raw) ? defaultValue : raw;
    }

    private async Task SetStringAsync(string key, string value, CancellationToken ct) =>
        await SetRawAsync(key, value, ct);

    private async Task<string?> GetRawAsync(string key, CancellationToken ct)
    {
        var found = await _repo.FindAsync(s => s.Key == key, ct);
        return found.FirstOrDefault()?.Value;
    }

    private async Task SetRawAsync(string key, string value, CancellationToken ct)
    {
        var existing = (await _repo.FindAsync(s => s.Key == key, ct)).FirstOrDefault();
        var row = existing ?? new Setting { Key = key };
        row.Value = value;
        row.UpdatedAt = DateTime.UtcNow;
        await _repo.UpsertAsync(row, ct);
    }
}

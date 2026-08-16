using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pomodoro.Application.DTOs;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Application.Services;

/// <summary>
/// Aggregates sessions and break-activity rows into a DailyReport.
/// </summary>
public sealed class ReportingService : IReportingService
{
    private readonly IRepository<PomodoroSession> _sessions;
    private readonly IRepository<BreakActivity> _activities;
    private readonly IRepository<TaskItem> _tasks;
    private readonly IRepository<DailyReport> _reports;
    private readonly ILogger<ReportingService> _logger;

    public ReportingService(
        IRepository<PomodoroSession> sessions,
        IRepository<BreakActivity> activities,
        IRepository<TaskItem> tasks,
        IRepository<DailyReport> reports,
        ILogger<ReportingService> logger)
    {
        _sessions = sessions;
        _activities = activities;
        _tasks = tasks;
        _reports = reports;
        _logger = logger;
    }

    public async Task<DailyReport> GetDailyReportAsync(DateTime localDate, CancellationToken ct = default)
    {
        var date = localDate.Date;
        // Always regenerate for today since data changes throughout the day
        if (date == DateTime.Today)
            return await RegenerateDailyReportAsync(localDate, ct);

        var existing = (await _reports.FindAsync(r => r.Date == date, ct)).FirstOrDefault();
        // Reports cached before the hourly timeline fields existed hold empty
        // arrays — regenerate them once from the still-available session rows.
        if (existing is not null
            && existing.HourlyFocusMinutesJson != "[]"
            && existing.HourlyBreakMinutesJson != "[]") return existing;
        return await RegenerateDailyReportAsync(localDate, ct);
    }

    public async Task<DailyReport> RegenerateDailyReportAsync(DateTime localDate, CancellationToken ct = default)
    {
        // Sessions are stored in UTC, so convert local day boundaries to UTC for querying
        var date = localDate.Date;
        var dayStartUtc = date.ToUniversalTime();
        var dayEndUtc = date.AddDays(1).ToUniversalTime();

        var allSessions = await _sessions.FindAsync(
            s => s.StartedAt >= dayStartUtc && s.StartedAt < dayEndUtc, ct);
        var allActivities = await _activities.FindAsync(
            a => a.CapturedAt >= dayStartUtc && a.CapturedAt < dayEndUtc, ct);

        var focusSessions = allSessions.Where(s => s.Phase == SessionPhase.FocusRunning).ToList();
        var breakSessions = allSessions.Where(s => s.Phase == SessionPhase.BreakRunning).ToList();

        var totalFocusSec = focusSessions.Sum(s => s.ActualDurationSec > 0 ? s.ActualDurationSec : s.PlannedDurationSec);
        var totalBreakSec = breakSessions.Sum(s => s.ActualDurationSec > 0 ? s.ActualDurationSec : s.PlannedDurationSec);

        var completedFocus = focusSessions.Count(s => s.WasCompleted);
        var totalKeys = allActivities.Sum(a => a.KeyPressCount);
        var totalClicks = allActivities.Sum(a => a.MouseClickCount);
        var totalIdle = allActivities.Sum(a => a.IdleSeconds);

        // Per-task breakdown
        var taskGroups = focusSessions
            .Where(s => s.TaskId.HasValue)
            .GroupBy(s => s.TaskId!.Value)
            .ToList();
        var breakdown = new List<(Guid TaskId, int Secs, int Count)>();
        foreach (var g in taskGroups)
        {
            breakdown.Add((g.Key, g.Sum(s => s.ActualDurationSec > 0 ? s.ActualDurationSec : s.PlannedDurationSec), g.Count()));
        }
        var taskIds = breakdown.Select(b => b.TaskId).Distinct().ToList();
        var taskLookup = new Dictionary<Guid, TaskItem>();
        foreach (var tid in taskIds)
        {
            var t = await _tasks.GetByIdAsync(tid, ct);
            if (t is not null) taskLookup[tid] = t;
        }

        var breakdownDtos = breakdown.Select(b => new TaskBreakdownDto
        {
            TaskId = b.TaskId,
            TaskTitle = taskLookup.TryGetValue(b.TaskId, out var t) ? t.Title : "(deleted)",
            MinutesSpent = b.Secs / 60,
            SessionCount = b.Count,
        }).ToList();
        var breakdownJson = JsonSerializer.Serialize(breakdownDtos, ReportJsonContext.Default.ListTaskBreakdownDto);

        // Hourly buckets
        var hourlyKeys = new int[24];
        var hourlyClicks = new int[24];
        foreach (var a in allActivities)
        {
            var hour = a.CapturedAt.ToLocalTime().Hour;
            hourlyKeys[hour] += a.KeyPressCount;
            hourlyClicks[hour] += a.MouseClickCount;
        }

        // Per-hour focus/break minutes from actual session times,
        // splitting sessions that span an hour boundary across both hours.
        var hourlyFocusMinutes = new int[24];
        var hourlyBreakMinutes = new int[24];
        foreach (var s in allSessions)
        {
            var isFocus = s.Phase == SessionPhase.FocusRunning;
            var seconds = s.ActualDurationSec > 0 ? s.ActualDurationSec : s.PlannedDurationSec;
            if (seconds <= 0) continue;

            var start = s.StartedAt.ToLocalTime();
            var end = start.AddSeconds(seconds);
            var cursor = start;
            while (cursor < end && cursor.Date == date)
            {
                var hourEnd = cursor.Date.AddHours(cursor.Hour + 1);
                var slice = (end < hourEnd ? end : hourEnd) - cursor;
                var wholeMinutes = (int)slice.TotalMinutes;
                if (wholeMinutes > 0)
                {
                    if (isFocus) hourlyFocusMinutes[cursor.Hour] += wholeMinutes;
                    else hourlyBreakMinutes[cursor.Hour] += wholeMinutes;
                }
                cursor = hourEnd;
            }
        }

        var report = new DailyReport
        {
            Date = date,
            CompletedFocusSessions = completedFocus,
            TotalFocusSeconds = totalFocusSec,
            TotalBreakSeconds = totalBreakSec,
            TotalKeystrokes = totalKeys,
            TotalMouseClicks = totalClicks,
            TotalIdleSeconds = totalIdle,
            TaskBreakdownJson = breakdownJson,
            HourlyKeystrokesJson = JsonSerializer.Serialize(hourlyKeys, ReportJsonContext.Default.Int32Array),
            HourlyMouseClicksJson = JsonSerializer.Serialize(hourlyClicks, ReportJsonContext.Default.Int32Array),
            HourlyFocusMinutesJson = JsonSerializer.Serialize(hourlyFocusMinutes, ReportJsonContext.Default.Int32Array),
            HourlyBreakMinutesJson = JsonSerializer.Serialize(hourlyBreakMinutes, ReportJsonContext.Default.Int32Array),
            GeneratedAt = DateTime.UtcNow,
        };

        // Upsert by date
        var existing = (await _reports.FindAsync(r => r.Date == date, ct)).FirstOrDefault();
        if (existing is not null)
        {
            report.Id = existing.Id;
        }
        await _reports.UpsertAsync(report, ct);
        _logger.LogInformation("Daily report generated for {Date}: {Sessions} focus sessions", date, completedFocus);
        return report;
    }
}

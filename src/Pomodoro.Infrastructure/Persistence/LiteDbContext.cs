using LiteDB;
using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;

namespace Pomodoro.Infrastructure.Persistence;

/// <summary>
/// Owns the LiteDatabase instance and exposes typed collections.
/// Performs AOT-safe explicit BsonMapper configuration (no reflection).
/// </summary>
public sealed class LiteDbContext : IDisposable, IAsyncDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILogger<LiteDbContext> _logger;

    public ILiteCollection<TaskItem> Tasks { get; }
    public ILiteCollection<PomodoroSession> Sessions { get; }
    public ILiteCollection<BreakActivity> Activities { get; }
    public ILiteCollection<Setting> Settings { get; }
    public ILiteCollection<DailyReport> Reports { get; }

    public LiteDbContext(string dbPath, ILogger<LiteDbContext> logger)
    {
        _logger = logger;
        var mapper = BuildAotSafeMapper();

        // Ensure directory exists
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _db = new LiteDatabase($"Filename={dbPath};Connection=shared", mapper);

        Tasks = _db.GetCollection<TaskItem>("tasks");
        Sessions = _db.GetCollection<PomodoroSession>("sessions");
        Activities = _db.GetCollection<BreakActivity>("activities");
        Settings = _db.GetCollection<Setting>("settings");
        Reports = _db.GetCollection<DailyReport>("reports");

        EnsureIndexes();
        _logger.LogInformation("LiteDB opened at {Path}", dbPath);
    }

    private static BsonMapper BuildAotSafeMapper()
    {
        var mapper = new BsonMapper();

        mapper.Entity<TaskItem>()
            .Id(x => x.Id)
            .Field(x => x.Title, "title")
            .Field(x => x.Description, "desc")
            .Field(x => x.Priority, "priority")
            .Field(x => x.Status, "status")
            .Field(x => x.CreatedAt, "created")
            .Field(x => x.CompletedAt, "completed")
            .Field(x => x.EstimatedPomodoros, "est")
            .Field(x => x.CompletedPomodoros, "done")
            .Field(x => x.SessionIds, "sessions");

        mapper.Entity<PomodoroSession>()
            .Id(x => x.Id)
            .Field(x => x.TaskId, "task_id")
            .Field(x => x.Phase, "phase")
            .Field(x => x.StartedAt, "start")
            .Field(x => x.EndedAt, "end")
            .Field(x => x.PlannedDurationSec, "planned")
            .Field(x => x.ActualDurationSec, "actual")
            .Field(x => x.WasCompleted, "completed")
            .Field(x => x.AbandonReason, "reason")
            .Field(x => x.CycleIndex, "cycle")
            .Field(x => x.IsLongBreak, "long_break");

        mapper.Entity<BreakActivity>()
            .Id(x => x.Id)
            .Field(x => x.BreakSessionId, "break_id")
            .Field(x => x.CapturedAt, "captured")
            .Field(x => x.KeyPressCount, "keys")
            .Field(x => x.MouseClickCount, "clicks")
            .Field(x => x.MouseDistancePx, "mouse_px")
            .Field(x => x.IdleSeconds, "idle");

        mapper.Entity<Setting>()
            .Id(x => x.Key)
            .Field(x => x.Value, "value")
            .Field(x => x.UpdatedAt, "updated");

        mapper.Entity<DailyReport>()
            .Id(x => x.Id)
            .Field(x => x.Date, "date")
            .Field(x => x.CompletedFocusSessions, "completed_focus")
            .Field(x => x.TotalFocusSeconds, "focus_sec")
            .Field(x => x.TotalBreakSeconds, "break_sec")
            .Field(x => x.TotalKeystrokes, "keys")
            .Field(x => x.TotalMouseClicks, "clicks")
            .Field(x => x.TotalIdleSeconds, "idle_sec")
            .Field(x => x.TaskBreakdownJson, "breakdown")
            .Field(x => x.HourlyKeystrokesJson, "hourly_keys")
            .Field(x => x.HourlyMouseClicksJson, "hourly_clicks")
            .Field(x => x.GeneratedAt, "generated");

        return mapper;
    }

    private void EnsureIndexes()
    {
        Tasks.EnsureIndex(x => x.Status);
        Tasks.EnsureIndex(x => x.CreatedAt);
        Sessions.EnsureIndex(x => x.StartedAt);
        Sessions.EnsureIndex(x => x.TaskId);
        Sessions.EnsureIndex(x => x.Phase);
        Activities.EnsureIndex(x => x.BreakSessionId);
        Activities.EnsureIndex(x => x.CapturedAt);
        Reports.EnsureIndex(x => x.Date, unique: true);
    }

    public void Dispose()
    {
        _db.Dispose();
        _logger.LogInformation("LiteDB closed");
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }
}

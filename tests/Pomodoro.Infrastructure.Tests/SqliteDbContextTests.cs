using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Interfaces;
using Pomodoro.Infrastructure.Persistence;
using Pomodoro.Infrastructure.Persistence.Mappers;
using Xunit;

namespace Pomodoro.Infrastructure.Tests;

/// <summary>
/// Integration tests against a real (temp-file) SQLite database.
/// Verifies that our AOT-safe mappers correctly serialize/deserialize entities.
/// </summary>
public class SqliteDbContextTests : IDisposable
{
    private readonly string _tempPath;
    private readonly SqliteDbContext _ctx;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<PomodoroSession> _sessionRepo;
    private readonly IRepository<Setting> _settingRepo;
    private readonly IRepository<DailyReport> _reportRepo;
    private readonly IRepository<BreakActivity> _activityRepo;

    public SqliteDbContextTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"pomodoro-test-{Guid.NewGuid():N}.db");
        _ctx = new SqliteDbContext(
            _tempPath,
            new TaskItemMapper(),
            new PomodoroSessionMapper(),
            new BreakActivityMapper(),
            new SettingMapper(),
            new DailyReportMapper(),
            NullLogger<SqliteDbContext>.Instance);
        _ctx.InitializeAsync().GetAwaiter().GetResult();

        _taskRepo = new SqliteRepository<TaskItem>(_ctx, new TaskItemMapper(),
            NullLogger<SqliteRepository<TaskItem>>.Instance);
        _sessionRepo = new SqliteRepository<PomodoroSession>(_ctx, new PomodoroSessionMapper(),
            NullLogger<SqliteRepository<PomodoroSession>>.Instance);
        _settingRepo = new SqliteRepository<Setting>(_ctx, new SettingMapper(),
            NullLogger<SqliteRepository<Setting>>.Instance);
        _reportRepo = new SqliteRepository<DailyReport>(_ctx, new DailyReportMapper(),
            NullLogger<SqliteRepository<DailyReport>>.Instance);
        _activityRepo = new SqliteRepository<BreakActivity>(_ctx, new BreakActivityMapper(),
            NullLogger<SqliteRepository<BreakActivity>>.Instance);
    }

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TaskItem_RoundTrip_PreservesAllFields()
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Test task",
            Description = "test description",
            Priority = TaskPriority.High,
            Status = TaskItemStatus.InProgress,
            EstimatedPomodoros = 3,
            CompletedPomodoros = 1,
            CreatedAt = DateTime.UtcNow,
            SessionIds = new() { Guid.NewGuid(), Guid.NewGuid() },
        };

        await _taskRepo.UpsertAsync(task, CT);

        var loaded = await _taskRepo.GetByIdAsync(task.Id, CT);
        loaded.Should().NotBeNull();
        loaded!.Title.Should().Be("Test task");
        loaded.Description.Should().Be("test description");
        loaded.Priority.Should().Be(TaskPriority.High);
        loaded.Status.Should().Be(TaskItemStatus.InProgress);
        loaded.EstimatedPomodoros.Should().Be(3);
        loaded.CompletedPomodoros.Should().Be(1);
        loaded.SessionIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task PomodoroSession_RoundTrip()
    {
        var session = new PomodoroSession
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            Phase = SessionPhase.FocusRunning,
            StartedAt = DateTime.UtcNow,
            PlannedDurationSec = 1500,
            CycleIndex = 2,
            IsLongBreak = false,
        };

        await _sessionRepo.UpsertAsync(session, CT);
        var loaded = await _sessionRepo.GetByIdAsync(session.Id, CT);

        loaded.Should().NotBeNull();
        loaded!.Phase.Should().Be(SessionPhase.FocusRunning);
        loaded.TaskId.Should().Be(session.TaskId);
        loaded.CycleIndex.Should().Be(2);
    }

    [Fact]
    public async Task Setting_KeyValue_RoundTrip()
    {
        var setting = new Setting { Key = "focus_duration", Value = "00:25:00" };
        await _settingRepo.UpsertAsync(setting, CT);

        var loaded = (await _settingRepo.FindAsync(s => s.Key == "focus_duration", CT)).FirstOrDefault();
        loaded.Should().NotBeNull();
        loaded!.Value.Should().Be("00:25:00");
    }

    [Fact]
    public async Task DailyReport_DateUniqueConstraint_OnlyOneRowPerDate()
    {
        var date = DateTime.UtcNow.Date;
        var r1 = new DailyReport { Id = Guid.NewGuid(), Date = date, TotalFocusSeconds = 100 };
        await _reportRepo.UpsertAsync(r1, CT);

        // Insert r2 with the SAME date but different Id.
        // INSERT OR REPLACE will detect the UNIQUE violation on Date, delete r1,
        // and insert r2 — so the table always has at most one row per date.
        var r2 = new DailyReport { Id = Guid.NewGuid(), Date = date, TotalFocusSeconds = 200 };
        await _reportRepo.UpsertAsync(r2, CT);

        // Verify: querying by date returns exactly one row (the latest one).
        var rows = await _reportRepo.FindAsync(r => r.Date == date, CT);
        rows.Should().HaveCount(1);
        rows[0].TotalFocusSeconds.Should().Be(200);

        // Verify a raw INSERT (without OR REPLACE) DOES throw — proves the UNIQUE index works.
        await using var cmd = ((SqliteDbContext)_ctx).Connection.CreateCommand();
        cmd.CommandText = "INSERT INTO reports (Id, Date, CompletedFocusSessions, TotalFocusSeconds, TotalBreakSeconds, TotalKeystrokes, TotalMouseClicks, TotalIdleSeconds, TaskBreakdownJson, HourlyKeystrokesJson, HourlyMouseClicksJson, GeneratedAt) VALUES (@id, @date, 0, 0, 0, 0, 0, 0, '[]', '[]', '[]', @ts);";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@date", date.ToString("O"));
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O"));
        var act = async () => await cmd.ExecuteNonQueryAsync(CT);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task BreakActivity_RoundTrip()
    {
        var a = new BreakActivity
        {
            Id = Guid.NewGuid(),
            BreakSessionId = Guid.NewGuid(),
            CapturedAt = DateTime.UtcNow,
            KeyPressCount = 42,
            MouseClickCount = 10,
            MouseDistancePx = 5000,
            IdleSeconds = 3,
        };
        await _activityRepo.UpsertAsync(a, CT);

        var loaded = await _activityRepo.GetByIdAsync(a.Id, CT);
        loaded.Should().NotBeNull();
        loaded!.KeyPressCount.Should().Be(42);
        loaded.MouseClickCount.Should().Be(10);
        loaded.MouseDistancePx.Should().Be(5000);
        loaded.IdleSeconds.Should().Be(3);
    }

    [Fact]
    public async Task FindAsync_FiltersInMemory()
    {
        // Insert 3 tasks with different statuses
        await _taskRepo.UpsertAsync(new TaskItem { Id = Guid.NewGuid(), Title = "A", Status = TaskItemStatus.Pending });
        await _taskRepo.UpsertAsync(new TaskItem { Id = Guid.NewGuid(), Title = "B", Status = TaskItemStatus.Completed });
        await _taskRepo.UpsertAsync(new TaskItem { Id = Guid.NewGuid(), Title = "C", Status = TaskItemStatus.Pending });

        var pending = await _taskRepo.FindAsync(t => t.Status == TaskItemStatus.Pending, CT);
        pending.Should().HaveCount(2);

        var all = await _taskRepo.GetAllAsync(CT);
        all.Should().HaveCount(3);
    }

    [Fact]
    public async Task CountAsync_ReturnsTotal()
    {
        await _taskRepo.UpsertAsync(new TaskItem { Id = Guid.NewGuid(), Title = "X" });
        await _taskRepo.UpsertAsync(new TaskItem { Id = Guid.NewGuid(), Title = "Y" });

        var total = await _taskRepo.CountAsync(null, CT);
        total.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRow()
    {
        var t = new TaskItem { Id = Guid.NewGuid(), Title = "to delete" };
        await _taskRepo.UpsertAsync(t, CT);

        await _taskRepo.DeleteAsync(t.Id, CT);

        var loaded = await _taskRepo.GetByIdAsync(t.Id, CT);
        loaded.Should().BeNull();
    }

    public void Dispose()
    {
        _ctx.Dispose();
        try { File.Delete(_tempPath); } catch { /* ignore */ }
    }
}

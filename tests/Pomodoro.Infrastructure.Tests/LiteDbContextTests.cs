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
using Xunit;

namespace Pomodoro.Infrastructure.Tests;

/// <summary>
/// Integration tests against a real (temp-file) LiteDB database.
/// Verifies that our AOT-safe BsonMapper correctly serializes/deserializes entities.
/// </summary>
public class LiteDbContextTests : IDisposable
{
    private readonly string _tempPath;
    private readonly LiteDbContext _ctx;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<PomodoroSession> _sessionRepo;
    private readonly IRepository<Setting> _settingRepo;
    private readonly IRepository<DailyReport> _reportRepo;
    private readonly IRepository<BreakActivity> _activityRepo;

    public LiteDbContextTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"pomodoro-test-{Guid.NewGuid():N}.db");
        _ctx = new LiteDbContext(_tempPath, NullLogger<LiteDbContext>.Instance);
        _taskRepo = new LiteRepository<TaskItem>(_ctx.Tasks);
        _sessionRepo = new LiteRepository<PomodoroSession>(_ctx.Sessions);
        _settingRepo = new LiteRepository<Setting>(_ctx.Settings);
        _reportRepo = new LiteRepository<DailyReport>(_ctx.Reports);
        _activityRepo = new LiteRepository<BreakActivity>(_ctx.Activities);
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
    public async Task DailyReport_DateUniqueIndex_EnforcesOne()
    {
        var date = DateTime.UtcNow.Date;
        var r1 = new DailyReport { Date = date, TotalFocusSeconds = 100 };
        await _reportRepo.UpsertAsync(r1, CT);

        var act = async () =>
        {
            var r2 = new DailyReport { Date = date, TotalFocusSeconds = 200 };
            await _reportRepo.UpsertAsync(r2, CT);
        };
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

    public void Dispose()
    {
        _ctx.Dispose();
        try { File.Delete(_tempPath); } catch { /* ignore */ }
    }
}

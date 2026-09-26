using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pomodoro.Application.DTOs;
using Pomodoro.Application.Engines;
using Pomodoro.Application.Services;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Interfaces;
using Pomodoro.Infrastructure.Persistence;
using Pomodoro.Infrastructure.Persistence.Mappers;
using Xunit;

namespace Pomodoro.Infrastructure.Tests;

/// <summary>
/// Full-stack reproduction of the swapped task-usage report bug:
/// PomodoroEngine writes sessions to a real SQLite database, then
/// ReportingService aggregates them back into the chart data the
/// DailyReportViewModel renders.
/// </summary>
public class ReportTaskMappingTests : IDisposable
{
    private readonly string _tempPath;
    private readonly SqliteDbContext _ctx;
    private readonly SqliteRepository<PomodoroSession> _sessionRepo;
    private readonly SqliteRepository<TaskItem> _taskRepo;
    private readonly SqliteRepository<BreakActivity> _activityRepo;
    private readonly SqliteRepository<DailyReport> _reportRepo;
    private readonly ISettingsService _settings;
    private readonly PomodoroEngine _engine;
    private readonly ReportingService _reporting;

    public ReportTaskMappingTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"pomodoro-report-{Guid.NewGuid():N}.db");
        _ctx = new SqliteDbContext(
            _tempPath,
            new TaskItemMapper(),
            new PomodoroSessionMapper(),
            new BreakActivityMapper(),
            new SettingMapper(),
            new DailyReportMapper(),
            NullLogger<SqliteDbContext>.Instance);
        _ctx.InitializeAsync().GetAwaiter().GetResult();

        _sessionRepo = new SqliteRepository<PomodoroSession>(_ctx, new PomodoroSessionMapper(),
            NullLogger<SqliteRepository<PomodoroSession>>.Instance);
        _taskRepo = new SqliteRepository<TaskItem>(_ctx, new TaskItemMapper(),
            NullLogger<SqliteRepository<TaskItem>>.Instance);
        _activityRepo = new SqliteRepository<BreakActivity>(_ctx, new BreakActivityMapper(),
            NullLogger<SqliteRepository<BreakActivity>>.Instance);
        _reportRepo = new SqliteRepository<DailyReport>(_ctx, new DailyReportMapper(),
            NullLogger<SqliteRepository<DailyReport>>.Instance);

        _settings = Substitute.For<ISettingsService>();
        SetFocusDuration(TimeSpan.FromMinutes(3));
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

        _engine = new PomodoroEngine(
            _settings,
            _sessionRepo,
            Substitute.For<INotificationService>(),
            Substitute.For<ISoundPlayer>(),
            Substitute.For<IActivityTracker>(),
            NullLogger<PomodoroEngine>.Instance);

        _reporting = new ReportingService(
            _sessionRepo, _activityRepo, _taskRepo, _reportRepo,
            NullLogger<ReportingService>.Instance);
    }

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private void SetFocusDuration(TimeSpan duration) =>
        _settings.GetFocusDurationAsync(Arg.Any<CancellationToken>()).Returns(duration);

    [Fact]
    public async Task Report_AttributesSessionsToTheirOwnTasks()
    {
        var task = new TaskItem { Id = Guid.NewGuid(), Title = "develop pomodoro" };
        await _taskRepo.UpsertAsync(task, CT);

        // Repro steps 1–2: run WITHOUT selecting a task, then stop.
        // Stopped with ~0s actual → report falls back to PlannedDurationSec (3 min).
        await _engine.StartFocusAsync(null, CT);
        await _engine.StopAsync(CT);

        // Repro steps 3–4: select the task (MainViewModel.SetActiveTask via
        // TaskListViewModel.UseForFocus) and run with it.
        SetFocusDuration(TimeSpan.FromMinutes(25));
        await _engine.StartFocusAsync(task.Id, CT);
        await _engine.SkipCurrentPhaseAsync(CT);

        var report = await _reporting.GetDailyReportAsync(DateTime.Today, CT);
        var breakdown = JsonSerializer.Deserialize(report.TaskBreakdownJson,
            ReportJsonContext.Default.ListTaskBreakdownDto) ?? new();

        breakdown.Should().HaveCount(2, "the no-task run and the tasked run must form separate groups");

        var noTask = breakdown.Single(b => b.TaskId == Guid.Empty);
        noTask.TaskTitle.Should().Be("(no task)");
        noTask.SessionCount.Should().Be(1);
        noTask.MinutesSpent.Should().Be(3);

        var withTask = breakdown.Single(b => b.TaskId == task.Id);
        withTask.TaskTitle.Should().Be("develop pomodoro");
        withTask.SessionCount.Should().Be(1);
        withTask.MinutesSpent.Should().Be(25);
    }

    [Fact]
    public async Task Report_CompletedFocusSession_KeepsTaskMapping()
    {
        // Same repro but run2 completes through the real OnPhaseCompletedAsync
        // path (2-second focus, ticked manually) instead of being skipped.
        var task = new TaskItem { Id = Guid.NewGuid(), Title = "develop pomodoro" };
        await _taskRepo.UpsertAsync(task, CT);

        await _engine.StartFocusAsync(null, CT);
        await _engine.StopAsync(CT);

        SetFocusDuration(TimeSpan.FromSeconds(2));
        await _engine.StartFocusAsync(task.Id, CT);
        await Task.Delay(TimeSpan.FromSeconds(2.2), CT);
        await _engine.OnSecondTickAsync(CT);

        _engine.CurrentPhase.Should().Be(SessionPhase.BreakRunning,
            "the completed focus session must transition into the break");

        var report = await _reporting.GetDailyReportAsync(DateTime.Today, CT);
        var breakdown = JsonSerializer.Deserialize(report.TaskBreakdownJson,
            ReportJsonContext.Default.ListTaskBreakdownDto) ?? new();

        var withTask = breakdown.Single(b => b.TaskId == task.Id);
        withTask.TaskTitle.Should().Be("develop pomodoro");
        withTask.SessionCount.Should().Be(1);

        var noTask = breakdown.Single(b => b.TaskId == Guid.Empty);
        noTask.TaskTitle.Should().Be("(no task)");
        noTask.SessionCount.Should().Be(1);
    }

    public void Dispose()
    {
        _ctx.Dispose();
        try { File.Delete(_tempPath); } catch { /* ignore */ }
    }
}

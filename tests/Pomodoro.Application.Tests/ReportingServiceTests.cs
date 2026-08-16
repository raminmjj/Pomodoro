using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pomodoro.Application.DTOs;
using Pomodoro.Application.Services;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Interfaces;
using Xunit;

namespace Pomodoro.Application.Tests;

public class ReportingServiceTests
{
    private readonly IRepository<PomodoroSession> _sessions;
    private readonly IRepository<BreakActivity> _activities;
    private readonly IRepository<TaskItem> _tasks;
    private readonly IRepository<DailyReport> _reports;
    private readonly ReportingService _svc;

    public ReportingServiceTests()
    {
        _sessions = Substitute.For<IRepository<PomodoroSession>>();
        _activities = Substitute.For<IRepository<BreakActivity>>();
        _tasks = Substitute.For<IRepository<TaskItem>>();
        _reports = Substitute.For<IRepository<DailyReport>>();
        _svc = new ReportingService(
            _sessions, _activities, _tasks, _reports, NullLogger<ReportingService>.Instance);
    }

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetDailyReportAsync_IncludesNoTaskSessionsInBreakdown()
    {
        // Local day boundaries computed the same way the service does,
        // so the sessions land inside the queried window on any timezone.
        var localDate = new DateTime(2026, 8, 16);
        var dayStartUtc = localDate.ToUniversalTime();
        var taskId = Guid.NewGuid();

        _sessions.FindAsync(Arg.Any<Expression<Func<PomodoroSession, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new PomodoroSession
                {
                    Phase = SessionPhase.FocusRunning, TaskId = taskId,
                    StartedAt = dayStartUtc.AddHours(1).ToUniversalTime(),
                    PlannedDurationSec = 300, ActualDurationSec = 300, WasCompleted = true,
                },
                new PomodoroSession
                {
                    Phase = SessionPhase.FocusRunning, TaskId = null,
                    StartedAt = dayStartUtc.AddHours(3).ToUniversalTime(),
                    PlannedDurationSec = 180, ActualDurationSec = 180, WasCompleted = true,
                },
            });
        _activities.FindAsync(Arg.Any<Expression<Func<BreakActivity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BreakActivity>());
        _tasks.GetByIdAsync(taskId, Arg.Any<CancellationToken>())
            .Returns(new TaskItem { Id = taskId, Title = "Work" });
        _reports.FindAsync(Arg.Any<Expression<Func<DailyReport, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DailyReport>());

        var report = await _svc.GetDailyReportAsync(localDate, CT);

        report.TotalFocusSeconds.Should().Be(480);

        var breakdown = JsonSerializer.Deserialize(report.TaskBreakdownJson, ReportJsonContext.Default.ListTaskBreakdownDto)
            ?? new();
        breakdown.Should().HaveCount(2);
        breakdown.Should().ContainSingle(b => b.TaskTitle == "Work" && b.MinutesSpent == 5);
        breakdown.Should().ContainSingle(b => b.TaskTitle == "(no task)" && b.TaskId == Guid.Empty && b.MinutesSpent == 3);
    }
}

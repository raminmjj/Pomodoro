using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pomodoro.Application.Services;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Interfaces;
using Xunit;

namespace Pomodoro.Application.Tests;

public class TaskServiceTests
{
    private readonly IRepository<TaskItem> _repo;
    private readonly TaskService _svc;

    public TaskServiceTests()
    {
        _repo = Substitute.For<IRepository<TaskItem>>();
        _svc = new TaskService(_repo, NullLogger<TaskService>.Instance);
    }

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateAsync_ThrowsForEmptyTitle()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _svc.CreateAsync("", null, CT));
        await Assert.ThrowsAsync<ArgumentException>(() => _svc.CreateAsync("   ", null, CT));
    }

    [Fact]
    public async Task CreateAsync_PersistsAndReturnsTask()
    {
        var task = await _svc.CreateAsync("Test task", "description", CT);
        task.Title.Should().Be("Test task");
        task.Description.Should().Be("description");
        task.Status.Should().Be(TaskItemStatus.Pending);
        await _repo.Received(1).UpsertAsync(
            Arg.Is<TaskItem>(t => t.Title == "Test task"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_ThrowsForUnknownId()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TaskItem?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _svc.CompleteAsync(Guid.NewGuid(), CT));
    }

    [Fact]
    public async Task CompleteAsync_SetsCompletedAtAndStatus()
    {
        var task = new TaskItem { Id = Guid.NewGuid(), Title = "T" };
        _repo.GetByIdAsync(task.Id, Arg.Any<CancellationToken>())
            .Returns(task);

        await _svc.CompleteAsync(task.Id, CT);

        task.Status.Should().Be(TaskItemStatus.Completed);
        task.CompletedAt.Should().NotBeNull();
        await _repo.Received(1).UpsertAsync(
            Arg.Is<TaskItem>(t => t.Status == TaskItemStatus.Completed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncrementPomodoroCount_IncrementsAndAddsSessionId()
    {
        var task = new TaskItem { Id = Guid.NewGuid(), Title = "T" };
        _repo.GetByIdAsync(task.Id, Arg.Any<CancellationToken>())
            .Returns(task);

        var sessionId = Guid.NewGuid();
        await _svc.IncrementPomodoroCountAsync(task.Id, sessionId, CT);

        task.CompletedPomodoros.Should().Be(1);
        task.Status.Should().Be(TaskItemStatus.InProgress);
        task.SessionIds.Should().Contain(sessionId);
    }
}

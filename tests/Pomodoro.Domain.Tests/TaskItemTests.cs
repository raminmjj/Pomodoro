using AwesomeAssertions;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;
using Xunit;

namespace Pomodoro.Domain.Tests;

public class TaskItemTests
{
    [Fact]
    public void Constructor_AssignsNewGuid()
    {
        var t = new TaskItem();
        t.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void IsActive_True_ForPendingAndInProgress()
    {
        new TaskItem { Status = TaskItemStatus.Pending }.IsActive.Should().BeTrue();
        new TaskItem { Status = TaskItemStatus.InProgress }.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_False_ForCompletedAndCancelled()
    {
        new TaskItem { Status = TaskItemStatus.Completed }.IsActive.Should().BeFalse();
        new TaskItem { Status = TaskItemStatus.Cancelled }.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Defaults_AreApplied()
    {
        var t = new TaskItem();
        t.EstimatedPomodoros.Should().Be(1);
        t.CompletedPomodoros.Should().Be(0);
        t.Status.Should().Be(TaskItemStatus.Pending);
        t.Priority.Should().Be(TaskPriority.Normal);
        t.SessionIds.Should().BeEmpty();
    }
}

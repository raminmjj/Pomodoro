using AwesomeAssertions;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;
using Xunit;

namespace Pomodoro.Domain.Tests;

public class PomodoroSessionTests
{
    [Fact]
    public void Constructor_AssignsNewGuid()
    {
        var s = new PomodoroSession();
        s.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Defaults_AreApplied()
    {
        var s = new PomodoroSession();
        s.Phase.Should().Be(SessionPhase.Idle);
        s.WasCompleted.Should().BeFalse();
        s.IsLongBreak.Should().BeFalse();
        s.CycleIndex.Should().Be(0);
    }
}

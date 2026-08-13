using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pomodoro.Application.Services;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Interfaces;
using Xunit;

namespace Pomodoro.Application.Tests;

public class SettingsServiceTests
{
    private readonly IRepository<Setting> _repo;
    private readonly SettingsService _svc;

    public SettingsServiceTests()
    {
        _repo = Substitute.For<IRepository<Setting>>();
        _svc = new SettingsService(_repo, NullLogger<SettingsService>.Instance);
    }

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetFocusDuration_DefaultsTo25Minutes_WhenNotSet()
    {
        _repo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Setting, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Setting>());

        var result = await _svc.GetFocusDurationAsync(CT);
        result.Should().Be(TimeSpan.FromMinutes(25));
    }

    [Fact]
    public async Task GetFocusDuration_ReturnsPersistedValue()
    {
        var setting = new Setting { Key = "focus_duration", Value = "00:30:00" };
        _repo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Setting, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { setting });

        var result = await _svc.GetFocusDurationAsync(CT);
        result.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task SetFocusDuration_PersistsNewRow_WhenNoneExists()
    {
        _repo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Setting, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Setting>());

        await _svc.SetFocusDurationAsync(TimeSpan.FromMinutes(45), CT);

        await _repo.Received(1).UpsertAsync(
            Arg.Is<Setting>(s => s.Key == "focus_duration" && s.Value == "00:45:00"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetFocusDuration_UpdatesExistingRow()
    {
        var existing = new Setting { Key = "focus_duration", Value = "00:25:00" };
        _repo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Setting, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { existing });

        await _svc.SetFocusDurationAsync(TimeSpan.FromMinutes(50), CT);

        await _repo.Received(1).UpsertAsync(
            Arg.Is<Setting>(s => s.Value == "00:50:00"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSessionsBeforeLongBreak_DefaultsTo4()
    {
        _repo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Setting, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Setting>());
        var result = await _svc.GetSessionsBeforeLongBreakAsync(CT);
        result.Should().Be(4);
    }
}

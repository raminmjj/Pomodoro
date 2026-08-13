using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pomodoro.Application.Services;
using Pomodoro.Domain.Interfaces;
using Xunit;

namespace Pomodoro.Application.Tests;

public class ActivityAlertEvaluatorTests
{
    private readonly ISettingsService _settings;
    private readonly ActivityAlertEvaluator _evaluator;

    public ActivityAlertEvaluatorTests()
    {
        _settings = Substitute.For<ISettingsService>();
        _settings.GetKeystrokeAlertThresholdAsync(Arg.Any<CancellationToken>())
            .Returns(60);
        _evaluator = new ActivityAlertEvaluator(_settings, NullLogger<ActivityAlertEvaluator>.Instance);
    }

    [Fact]
    public async Task Evaluate_BelowThreshold_DoesNotRaise()
    {
        var raised = false;
        _evaluator.AlertRaised += (_, _) => raised = true;

        // Prime the window with 35 seconds of low activity (need >= 30 to start evaluating)
        for (int i = 0; i < 35; i++)
        {
            await _evaluator.EvaluateAsync(1, 0, 60, CancellationToken.None);
        }
        raised.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_AboveThreshold_RaisesAlert_After30SecondsOfData()
    {
        var raised = false;
        _evaluator.AlertRaised += (_, _) => raised = true;

        // Push 35 seconds of high keystroke count (>60)
        for (int i = 0; i < 35; i++)
        {
            await _evaluator.EvaluateAsync(5, 0, 5, CancellationToken.None);
        }
        raised.Should().BeTrue();
    }

    [Fact]
    public async Task Evaluate_Cooldown_PreventsImmediateReAlert()
    {
        var alertCount = 0;
        _evaluator.AlertRaised += (_, _) => alertCount++;

        // First batch triggers
        for (int i = 0; i < 35; i++)
            await _evaluator.EvaluateAsync(5, 0, 5, CancellationToken.None);
        alertCount.Should().Be(1);

        // Second batch within cooldown should not trigger
        for (int i = 0; i < 35; i++)
            await _evaluator.EvaluateAsync(5, 0, 5, CancellationToken.None);
        alertCount.Should().Be(1);
    }

    [Fact]
    public void Reset_ClearsWindowState()
    {
        // Just ensure Reset doesn't throw
        _evaluator.Reset();
    }
}

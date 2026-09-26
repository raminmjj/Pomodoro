using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using LiveChartsCore.SkiaSharpView;
using NSubstitute;
using Pomodoro.Application.DTOs;
using Pomodoro.App.Services;
using Pomodoro.App.ViewModels;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Interfaces;
using Xunit;

namespace Pomodoro.App.Tests.E2E;

/// <summary>
/// Guards the chart data pairing in DailyReportViewModel: every chart must show
/// each task's title next to that task's own minutes — never a neighbour's.
/// </summary>
public class DailyReportViewModelTests
{
    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private static DailyReport MakeReport()
    {
        var breakdown = new System.Collections.Generic.List<TaskBreakdownDto>
        {
            new() { TaskId = Guid.Empty, TaskTitle = "(no task)", MinutesSpent = 3, SessionCount = 1 },
            new() { TaskId = Guid.NewGuid(), TaskTitle = "develop pomodoro", MinutesSpent = 25, SessionCount = 1 },
        };

        return new DailyReport
        {
            TaskBreakdownJson = JsonSerializer.Serialize(breakdown, ReportJsonContext.Default.ListTaskBreakdownDto),
            HourlyKeystrokesJson = JsonSerializer.Serialize(new int[24], ReportJsonContext.Default.Int32Array),
            HourlyMouseClicksJson = JsonSerializer.Serialize(new int[24], ReportJsonContext.Default.Int32Array),
            HourlyFocusMinutesJson = JsonSerializer.Serialize(new int[24], ReportJsonContext.Default.Int32Array),
            HourlyBreakMinutesJson = JsonSerializer.Serialize(new int[24], ReportJsonContext.Default.Int32Array),
        };
    }

    [Fact]
    public async Task Charts_PairEachTaskTitleWithItsOwnMinutes()
    {
        var reporting = Substitute.For<IReportingService>();
        reporting.GetDailyReportAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(MakeReport());
        var navigation = Substitute.For<INavigationService>();

        var vm = new DailyReportViewModel(reporting, navigation);
        await vm.LoadReportAsync(DateTime.Today);

        // Pie ("Task Breakdown"): title and value live on the same series.
        var pie = vm.TaskBreakdownSeries.Cast<PieSeries<double>>().ToArray();
        pie.Should().HaveCount(2);
        pie.Single(s => s.Name == "(no task)").Values!.First().Should().Be(3);
        pie.Single(s => s.Name == "develop pomodoro").Values!.First().Should().Be(25);

        // Bar ("Time per Task"): axis labels and values are paired by index.
        var labels = vm.TaskActivityYAxes[0].Labels!;
        var values = ((RowSeries<double>)vm.TaskActivitySeries[0]).Values!.ToArray();
        labels.Should().Equal("(no task)", "develop pomodoro");
        values.Should().Equal(3d, 25d);

        // KPI card
        vm.TopTask.Should().Be("develop pomodoro");
    }
}

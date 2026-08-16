using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Measure;
using Pomodoro.App.Services;
using Pomodoro.Application.DTOs;
using Pomodoro.Domain.Interfaces;
using SkiaSharp;

namespace Pomodoro.App.ViewModels;

public sealed partial class DailyReportViewModel : BaseViewModel
{
    private readonly IReportingService _reporting;

    [ObservableProperty] private DateTimeOffset? _selectedDate = new DateTimeOffset(DateTime.Today);
    [ObservableProperty] private string _reportDate = string.Empty;
    [ObservableProperty] private string _totalFocusTime = "0h 0m";
    [ObservableProperty] private string _totalBreakTime = "0h 0m";
    [ObservableProperty] private string _completedSessions = "0";
    [ObservableProperty] private string _topTask = "—";
    [ObservableProperty] private string _totalKeystrokes = "0";
    [ObservableProperty] private string _totalMouseClicks = "0";

    partial void OnSelectedDateChanged(DateTimeOffset? value)
    {
        if (value.HasValue)
            _ = LoadReportAsync(value.Value.DateTime);
    }

    private ISeries[] _timelineSeries = Array.Empty<ISeries>();
    public ISeries[] TimelineSeries
    {
        get => _timelineSeries;
        private set => SetProperty(ref _timelineSeries, value);
    }

    private Axis[] _timelineXAxes = Array.Empty<Axis>();
    public Axis[] TimelineXAxes
    {
        get => _timelineXAxes;
        private set => SetProperty(ref _timelineXAxes, value);
    }

    private Axis[] _timelineYAxes = Array.Empty<Axis>();
    public Axis[] TimelineYAxes
    {
        get => _timelineYAxes;
        private set => SetProperty(ref _timelineYAxes, value);
    }

    private ISeries[] _taskBreakdownSeries = Array.Empty<ISeries>();
    public ISeries[] TaskBreakdownSeries
    {
        get => _taskBreakdownSeries;
        private set => SetProperty(ref _taskBreakdownSeries, value);
    }

    private ISeries[] _activitySeries = Array.Empty<ISeries>();
    public ISeries[] ActivitySeries
    {
        get => _activitySeries;
        private set => SetProperty(ref _activitySeries, value);
    }

    private Axis[] _activityXAxes = Array.Empty<Axis>();
    public Axis[] ActivityXAxes
    {
        get => _activityXAxes;
        private set => SetProperty(ref _activityXAxes, value);
    }

    private ISeries[] _taskActivitySeries = Array.Empty<ISeries>();
    public ISeries[] TaskActivitySeries
    {
        get => _taskActivitySeries;
        private set => SetProperty(ref _taskActivitySeries, value);
    }

    private Axis[] _taskActivityXAxes = Array.Empty<Axis>();
    public Axis[] TaskActivityXAxes
    {
        get => _taskActivityXAxes;
        private set => SetProperty(ref _taskActivityXAxes, value);
    }

    private Axis[] _taskActivityYAxes = Array.Empty<Axis>();
    public Axis[] TaskActivityYAxes
    {
        get => _taskActivityYAxes;
        private set => SetProperty(ref _taskActivityYAxes, value);
    }

    public DailyReportViewModel(IReportingService reporting, INavigationService navigation)
    {
        _reporting = reporting;
        navigation.ViewChanged += view =>
        {
            if (view == AppView.DailyReport)
                _ = LoadReportAsync(DateTime.Today);
        };
        _ = LoadReportAsync(DateTime.Today);
    }

    [RelayCommand]
    public async Task LoadReportAsync(DateTime date)
    {
        SelectedDate = new DateTimeOffset(date);
        await RunSafeAsync(async () =>
        {
            var report = await _reporting.GetDailyReportAsync(date);
            ReportDate = date.ToString("yyyy/MM/dd");
            TotalFocusTime = $"{report.TotalFocusSeconds / 3600}h {(report.TotalFocusSeconds % 3600) / 60}m";
            TotalBreakTime = $"{report.TotalBreakSeconds / 3600}h {(report.TotalBreakSeconds % 3600) / 60}m";
            CompletedSessions = report.CompletedFocusSessions.ToString();
            TotalKeystrokes = report.TotalKeystrokes.ToString("N0");
            TotalMouseClicks = report.TotalMouseClicks.ToString("N0");

            var breakdown = JsonSerializer.Deserialize(report.TaskBreakdownJson, ReportJsonContext.Default.ListTaskBreakdownDto)
                ?? new();
            TopTask = breakdown
                .Where(b => b.TaskId != Guid.Empty)
                .OrderByDescending(b => b.MinutesSpent)
                .FirstOrDefault()?.TaskTitle ?? "—";

            BuildTaskBreakdownSeries(breakdown);
            BuildTaskActivitySeries(breakdown);
            BuildTimelineSeries(report);

            var hourlyKeys = JsonSerializer.Deserialize(report.HourlyKeystrokesJson, ReportJsonContext.Default.Int32Array)
                ?? new int[24];
            var hourlyClicks = JsonSerializer.Deserialize(report.HourlyMouseClicksJson, ReportJsonContext.Default.Int32Array)
                ?? new int[24];
            BuildActivitySeries(hourlyKeys, hourlyClicks);
        });
    }

    /// <summary>
    /// Builds a timeline chart showing Focus and Break minutes across the day.
    /// Uses per-hour minutes computed from actual session times in the report.
    /// </summary>
    private void BuildTimelineSeries(Domain.Entities.DailyReport report)
    {
        var focusMinutes = JsonSerializer.Deserialize(report.HourlyFocusMinutesJson, ReportJsonContext.Default.Int32Array)
            ?? new int[24];
        var breakMinutes = JsonSerializer.Deserialize(report.HourlyBreakMinutesJson, ReportJsonContext.Default.Int32Array)
            ?? new int[24];

        TimelineSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Focus",
                Values = focusMinutes.Select(m => (double)m).ToArray(),
                Fill = new SolidColorPaint(new SKColor(0x1B, 0x6B, 0x7A)),
                MaxBarWidth = 14,
            },
            new ColumnSeries<double>
            {
                Name = "Break",
                Values = breakMinutes.Select(m => (double)m).ToArray(),
                Fill = new SolidColorPaint(new SKColor(0x37, 0xDC, 0xF2)),
                MaxBarWidth = 14,
            },
        };

        TimelineXAxes = new Axis[]
        {
            new Axis
            {
                Name = "Hour",
                Labels = Enumerable.Range(0, 24).Select(h => $"{h:00}").ToArray(),
                LabelsPaint = new SolidColorPaint(new SKColor(0x5B, 0x6B, 0x7D)),
                TextSize = 10,
            }
        };

        TimelineYAxes = new Axis[]
        {
            new Axis
            {
                Name = "Minutes",
                LabelsPaint = new SolidColorPaint(new SKColor(0x5B, 0x6B, 0x7D)),
                TextSize = 10,
            }
        };
    }

    private void BuildTaskBreakdownSeries(List<TaskBreakdownDto> breakdown)
    {
        if (breakdown.Count == 0)
        {
            TaskBreakdownSeries = Array.Empty<ISeries>();
            return;
        }

        var palette = new[] { SKColor.Parse("#1B6B7A"), SKColor.Parse("#37DCF2"), SKColor.Parse("#FFA500"),
                              SKColor.Parse("#94A3B8"), SKColor.Parse("#10B981"), SKColor.Parse("#F87171") };

        TaskBreakdownSeries = breakdown.Select((b, i) => new PieSeries<double>
        {
            Name = b.TaskTitle,
            Values = new double[] { b.MinutesSpent },
            Fill = new SolidColorPaint(palette[i % palette.Length]),
        }).Cast<ISeries>().ToArray();
    }

    /// <summary>
    /// Horizontal bar chart of focus minutes per task for the day.
    /// Ascending order so the largest task renders at the top of the chart
    /// (row series plot the first label at the bottom).
    /// </summary>
    private void BuildTaskActivitySeries(List<TaskBreakdownDto> breakdown)
    {
        if (breakdown.Count == 0)
        {
            TaskActivitySeries = Array.Empty<ISeries>();
            TaskActivityXAxes = Array.Empty<Axis>();
            TaskActivityYAxes = Array.Empty<Axis>();
            return;
        }

        var ordered = breakdown.OrderBy(b => b.MinutesSpent).ToList();
        TaskActivitySeries = new ISeries[]
        {
            new RowSeries<double>
            {
                Name = "Focus minutes",
                Values = ordered.Select(b => (double)b.MinutesSpent).ToArray(),
                Fill = new SolidColorPaint(new SKColor(0x1B, 0x6B, 0x7A)),
                MaxBarWidth = 18,
            },
        };

        TaskActivityYAxes = new Axis[]
        {
            new Axis
            {
                Labels = ordered.Select(b => b.TaskTitle).ToArray(),
                LabelsPaint = new SolidColorPaint(new SKColor(0x5B, 0x6B, 0x7D)),
                TextSize = 11,
            }
        };

        TaskActivityXAxes = new Axis[]
        {
            new Axis
            {
                Name = "Minutes",
                LabelsPaint = new SolidColorPaint(new SKColor(0x5B, 0x6B, 0x7D)),
                TextSize = 10,
            }
        };
    }

    private void BuildActivitySeries(int[] hourlyKeys, int[] hourlyClicks)
    {
        ActivitySeries = new ISeries[]
        {
            new ColumnSeries<int>
            {
                Name = "Keystrokes",
                Values = hourlyKeys,
                Fill = new SolidColorPaint(new SKColor(0x1B, 0x6B, 0x7A)),
                MaxBarWidth = 24,
            },
            new ColumnSeries<int>
            {
                Name = "Mouse Clicks",
                Values = hourlyClicks,
                Fill = new SolidColorPaint(new SKColor(0xFF, 0xA5, 0x00)),
                MaxBarWidth = 24,
            },
        };

        ActivityXAxes = new Axis[]
        {
            new Axis
            {
                Name = "Hour",
                Labels = Enumerable.Range(0, 24).Select(h => $"{h:00}").ToArray(),
                LabelsPaint = new SolidColorPaint(new SKColor(0x5B, 0x6B, 0x7D)),
                TextSize = 10,
            }
        };
    }

    [RelayCommand]
    private void Back() => ServiceLocator.GetRequiredService<INavigationService>().NavigateTo(AppView.Main);

}

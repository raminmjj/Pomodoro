using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Pomodoro.App.Services;
using Pomodoro.App.ViewModels;

namespace Pomodoro.App.Views;

public sealed partial class MainWindow : Window
{
    private MainViewModel? _mainVm;
    private TaskListViewModel? _taskListVm;
    private SettingsViewModel? _settingsVm;
    private DailyReportViewModel? _reportVm;
    private INavigationService? _navigation;

    public WindowNotificationManager NotificationMgr { get; }

    public MainWindow()
    {
        InitializeComponent();
        NotificationMgr = new WindowNotificationManager(this)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 3,
        };
    }

    public MainWindow(
        MainViewModel mainVm,
        TaskListViewModel taskListVm,
        SettingsViewModel settingsVm,
        DailyReportViewModel reportVm,
        INavigationService navigation) : this()
    {
        _mainVm = mainVm;
        _taskListVm = taskListVm;
        _settingsVm = settingsVm;
        _reportVm = reportVm;
        _navigation = navigation;

        // ← کلید fix: ست کردن DataContext روی Window
        // بدون این، binding‌های نوار پایین (GoToTasksCommand, GoToSettingsCommand, ...)
        // هرگز resolve نمی‌شوند و دکمه‌ها غیرفعال می‌مانند.
        DataContext = _mainVm;

        NavigateTo(AppView.Main);
        _navigation.ViewChanged += view => Avalonia.Threading.Dispatcher.UIThread.Post(() => NavigateTo(view));
    }

    private void NavigateTo(AppView view)
    {
        if (_mainVm is null) return;
        Control content = view switch
        {
            AppView.Main => new MainView { DataContext = _mainVm },
            AppView.TaskList => new TaskListView { DataContext = _taskListVm! },
            AppView.Settings => new SettingsView { DataContext = _settingsVm! },
            AppView.DailyReport => new DailyReportView { DataContext = _reportVm! },
            _ => new MainView { DataContext = _mainVm },
        };
        ContentHost.Content = content;
    }
}

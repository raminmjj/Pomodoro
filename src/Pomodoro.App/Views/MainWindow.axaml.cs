using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
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

    private TrayIcon? _trayIcon;
    private WindowState _restoreState = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();

        // Minimize button hides to the system tray instead of the taskbar.
        // The Hide must be deferred: calling it synchronously inside the
        // minimize command re-shows the window when SW_MINIMIZE completes.
        PropertyChanged += (_, e) =>
        {
            if (e.Property != WindowStateProperty) return;
            if (WindowState == WindowState.Minimized)
                Dispatcher.UIThread.Post(Hide, DispatcherPriority.Background);
            else
                _restoreState = WindowState;
        };
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null) return;

        using var stream = AssetLoader.Open(new Uri("avares://Pomodoro/Assets/appicon.png"));
        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(stream),
            ToolTipText = "Shahsavar Pomodoro 2500",
        };

        var openItem = new NativeMenuItem { Header = "Open" };
        openItem.Click += (_, _) => RestoreFromTray();
        var exitItem = new NativeMenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Close();
        _trayIcon.Menu = new NativeMenu { Items = { openItem, exitItem } };

        _trayIcon.Clicked += (_, _) => RestoreFromTray();
        _trayIcon.IsVisible = true;
    }

    private void RestoreFromTray()
    {
        WindowState = _restoreState;
        Show();
        Activate();
    }

    protected override void OnClosed(EventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnClosed(e);
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

        EnsureTrayIcon();
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

        ContentHost.Children.Clear();
        ContentHost.Children.Add(content);
    }
}

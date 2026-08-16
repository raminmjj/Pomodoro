using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Pomodoro.App.Services;
using Pomodoro.App.Views;
using Pomodoro.Infrastructure.Notifications;

namespace Pomodoro.App;

internal sealed class App : Avalonia.Application
{
    private readonly IServiceProvider _services;
    private readonly bool _startMinimized;
    private readonly CancellationTokenSource _shutdownCts;

    public App(IServiceProvider services, bool startMinimized, CancellationTokenSource shutdownCts)
    {
        _services = services;
        _startMinimized = startMinimized;
        _shutdownCts = shutdownCts;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = _services.GetRequiredService<MainWindow>();

            // Wire notification sink — OS-level desktop notifications
            // (Windows toast, macOS Notification Center, Linux notify-send)
            var notifService = _services.GetRequiredService<AvaloniaNotificationService>();
            notifService.Initialize(new DesktopNotificationSink(mainWindow));

            if (_startMinimized)
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.ShowInTaskbar = true;
            }

            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            Program.StartEngineTickLoop(_services, _shutdownCts.Token);
        }

        base.OnFrameworkInitializationCompleted();
    }
}

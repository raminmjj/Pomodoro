using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.DependencyInjection;
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

            // Wire notification sink
            var notifService = _services.GetRequiredService<AvaloniaNotificationService>();
            notifService.Initialize(new AvaloniaNotificationSink(mainWindow.NotificationMgr));

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

/// <summary>
/// Adapter from Avalonia WindowNotificationManager to INotificationSink.
/// </summary>
internal sealed class AvaloniaNotificationSink : INotificationSink
{
    private readonly WindowNotificationManager _manager;

    public AvaloniaNotificationSink(WindowNotificationManager manager)
    {
        _manager = manager;
    }

    public void Show(string title, string body, Pomodoro.Domain.Enums.NotificationSeverity severity)
    {
        var type = severity switch
        {
            Pomodoro.Domain.Enums.NotificationSeverity.Success => NotificationType.Success,
            Pomodoro.Domain.Enums.NotificationSeverity.Warning => NotificationType.Warning,
            Pomodoro.Domain.Enums.NotificationSeverity.Error => NotificationType.Error,
            _ => NotificationType.Information,
        };
        _manager.Show(new Notification(title, body, type));
    }
}

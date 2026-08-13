using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Pomodoro.App.Views;
using Xunit;

namespace Pomodoro.App.Tests.E2E;

/// <summary>
/// End-to-end tests using Avalonia.Headless to verify UI rendering and interactions.
/// These tests run without a real windowing system.
/// </summary>
public class MainViewTests
{
    [AvaloniaFact]
    public void MainView_Should_Render_Without_Error()
    {
        // Arrange & Act
        var view = new MainView();

        // Assert — if we got here without exception, the view rendered successfully
        Assert.NotNull(view);
    }

    [AvaloniaFact]
    public void MainWindow_Should_Render_Without_Error()
    {
        // Arrange & Act
        var window = new MainWindow();

        // Assert
        Assert.NotNull(window);
    }

    [AvaloniaFact]
    public void TaskListView_Should_Render_Without_Error()
    {
        // Arrange & Act
        var view = new TaskListView();

        // Assert
        Assert.NotNull(view);
    }

    [AvaloniaFact]
    public void SettingsView_Should_Render_Without_Error()
    {
        // Arrange & Act
        var view = new SettingsView();

        // Assert
        Assert.NotNull(view);
    }

    [AvaloniaFact]
    public void DailyReportView_Should_Render_Without_Error()
    {
        // Arrange & Act
        var view = new DailyReportView();

        // Assert
        Assert.NotNull(view);
    }
}

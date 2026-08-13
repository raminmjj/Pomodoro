namespace Pomodoro.App.Services;

public enum AppView { Main, TaskList, Settings, DailyReport }

public interface INavigationService
{
    void NavigateTo(AppView view);
    event Action<AppView>? ViewChanged;
}

internal sealed class NavigationService : INavigationService
{
    public event Action<AppView>? ViewChanged;
    public void NavigateTo(AppView view) => ViewChanged?.Invoke(view);
}

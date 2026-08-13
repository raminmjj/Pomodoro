using Avalonia.Controls;
using Avalonia.Input;

namespace Pomodoro.App.Views;

public sealed partial class TaskListView : UserControl
{
    public TaskListView() { InitializeComponent(); }

    private void OnAddKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ViewModels.TaskListViewModel vm)
        {
            _ = vm.AddCommand.ExecuteAsync(null);
        }
    }
}

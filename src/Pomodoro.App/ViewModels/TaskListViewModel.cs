using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pomodoro.App.Services;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Interfaces;
using TaskItem = Pomodoro.Domain.Entities.TaskItem;

namespace Pomodoro.App.ViewModels;

public sealed partial class TaskListViewModel : BaseViewModel
{
    private readonly ITaskService _taskService;
    private readonly INavigationService _navigation;

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    [ObservableProperty] private string _newTaskTitle = string.Empty;
    [ObservableProperty] private TaskItem? _selectedTask;

    public TaskListViewModel(ITaskService taskService, INavigationService navigation)
    {
        _taskService = taskService;
        _navigation = navigation;

        // Reload tasks every time the user navigates to this view
        navigation.ViewChanged += view =>
        {
            if (view == AppView.TaskList)
                _ = LoadAsync();
        };

        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunSafeAsync(async () =>
        {
            Tasks.Clear();
            var all = await _taskService.GetAllAsync();
            foreach (var t in all)
                Tasks.Add(t);
        });
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        await RunSafeAsync(async () =>
        {
            var t = await _taskService.CreateAsync(NewTaskTitle, null);
            Tasks.Insert(0, t);
            NewTaskTitle = string.Empty;
        });
    }

    [RelayCommand]
    private async Task CompleteAsync(TaskItem task)
    {
        await RunSafeAsync(async () =>
        {
            await _taskService.CompleteAsync(task.Id);
            task.Status = TaskItemStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            var idx = Tasks.IndexOf(task);
            if (idx >= 0) Tasks[idx] = task;
        });
    }

    [RelayCommand]
    private async Task DeleteAsync(TaskItem task)
    {
        await RunSafeAsync(async () =>
        {
            await _taskService.DeleteAsync(task.Id);
            Tasks.Remove(task);
        });
    }

    [RelayCommand]
    private void UseForFocus(TaskItem task)
    {
        SelectedTask = task;
        var mainVm = ServiceLocator.GetRequiredService<MainViewModel>();
        mainVm.SetActiveTask(task.Id, task.Title);
        _navigation.NavigateTo(AppView.Main);
    }

    [RelayCommand]
    private void Back() => _navigation.NavigateTo(AppView.Main);
}

using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Entities;
using Pomodoro.Domain.Enums;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Application.Services;

public sealed class TaskService : ITaskService
{
    private readonly IRepository<TaskItem> _repo;
    private readonly ILogger<TaskService> _logger;

    public TaskService(IRepository<TaskItem> repo, ILogger<TaskService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public Task<TaskItem?> GetAsync(Guid id, CancellationToken ct = default) =>
        _repo.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<TaskItem>> GetAllAsync(bool includeCompleted = true, CancellationToken ct = default)
    {
        if (includeCompleted) return _repo.GetAllAsync(ct);
        return _repo.FindAsync(t => t.Status != TaskItemStatus.Completed, ct);
    }

    public Task<IReadOnlyList<TaskItem>> GetActiveAsync(CancellationToken ct = default) =>
        _repo.FindAsync(t => t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress, ct);

    public async Task<TaskItem> CreateAsync(string title, string? description, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        var task = new TaskItem
        {
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Status = TaskItemStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        await _repo.UpsertAsync(task, ct);
        _logger.LogInformation("Task created: {Id} - {Title}", task.Id, task.Title);
        return task;
    }

    public async Task<TaskItem> UpdateAsync(Guid id, string title, string? description, CancellationToken ct = default)
    {
        var task = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Task {id} not found");
        task.Title = title.Trim();
        task.Description = description?.Trim() ?? string.Empty;
        await _repo.UpsertAsync(task, ct);
        return task;
    }

    public async Task CompleteAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Task {id} not found");
        task.Status = TaskItemStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        await _repo.UpsertAsync(task, ct);
        _logger.LogInformation("Task completed: {Id}", id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repo.DeleteAsync(id, ct);
        _logger.LogInformation("Task deleted: {Id}", id);
    }

    public async Task IncrementPomodoroCountAsync(Guid taskId, Guid sessionId, CancellationToken ct = default)
    {
        var task = await _repo.GetByIdAsync(taskId, ct);
        if (task is null) return;
        task.CompletedPomodoros++;
        task.Status = TaskItemStatus.InProgress;
        if (!task.SessionIds.Contains(sessionId))
            task.SessionIds.Add(sessionId);
        await _repo.UpsertAsync(task, ct);
    }
}

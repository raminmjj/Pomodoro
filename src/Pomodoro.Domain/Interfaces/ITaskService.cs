using Pomodoro.Domain.Entities;

namespace Pomodoro.Domain.Interfaces;

public interface ITaskService
{
    Task<TaskItem?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetAllAsync(bool includeCompleted = true, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetActiveAsync(CancellationToken ct = default);
    Task<TaskItem> CreateAsync(string title, string? description, CancellationToken ct = default);
    Task<TaskItem> UpdateAsync(Guid id, string title, string? description, CancellationToken ct = default);
    Task CompleteAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task IncrementPomodoroCountAsync(Guid taskId, Guid sessionId, CancellationToken ct = default);
}

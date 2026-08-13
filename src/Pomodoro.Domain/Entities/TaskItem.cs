using Pomodoro.Domain.Enums;

namespace Pomodoro.Domain.Entities;

/// <summary>
/// A user task that Pomodoro sessions can be tracked against.
/// </summary>
public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    /// <summary>How many Pomodoros the user thinks this task will take.</summary>
    public int EstimatedPomodoros { get; set; } = 1;

    /// <summary>How many Pomodoros have actually been logged against this task.</summary>
    public int CompletedPomodoros { get; set; } = 0;

    /// <summary>Session IDs linked to this task (denormalised for fast lookup).</summary>
    public List<Guid> SessionIds { get; set; } = new();

    public bool IsActive => Status is TaskItemStatus.Pending or TaskItemStatus.InProgress;

    public override string ToString() =>
        $"[#{Id.ToString().Substring(0, 8)}] {Title} ({Status}, {CompletedPomodoros}/{EstimatedPomodoros})";
}

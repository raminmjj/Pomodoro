namespace Pomodoro.Domain.Enums;

/// <summary>
/// Lifecycle status of a TaskItem.
/// </summary>
public enum TaskItemStatus
{
    /// <summary>Created but not yet started.</summary>
    Pending = 0,

    /// <summary>Currently being worked on.</summary>
    InProgress = 1,

    /// <summary>Finished.</summary>
    Completed = 2,

    /// <summary>Abandoned without completion.</summary>
    Cancelled = 3,
}

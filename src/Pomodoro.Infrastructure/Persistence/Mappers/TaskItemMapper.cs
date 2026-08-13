using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Pomodoro.Domain.Entities;

namespace Pomodoro.Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps TaskItem to/from the "tasks" SQLite table.
/// SessionIds (List&lt;Guid&gt;) is stored as JSON text.
/// </summary>
public sealed class TaskItemMapper : ISqliteMapper<TaskItem>
{
    public string TableName => "tasks";
    public string PrimaryKeyColumn => "Id";
    public bool HasGuidPrimaryKey => true;

    public string CreateTableSql => """
        CREATE TABLE IF NOT EXISTS tasks (
            Id TEXT PRIMARY KEY NOT NULL,
            Title TEXT NOT NULL,
            Description TEXT NOT NULL,
            Priority INTEGER NOT NULL,
            Status INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            CompletedAt TEXT,
            EstimatedPomodoros INTEGER NOT NULL,
            CompletedPomodoros INTEGER NOT NULL,
            SessionIds TEXT NOT NULL
        );
        """;

    public IReadOnlyList<string> CreateIndexSqls => new[]
    {
        "CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(Status);",
        "CREATE INDEX IF NOT EXISTS idx_tasks_created ON tasks(CreatedAt);",
    };

    public string UpsertSql => """
        INSERT OR REPLACE INTO tasks
            (Id, Title, Description, Priority, Status, CreatedAt, CompletedAt,
             EstimatedPomodoros, CompletedPomodoros, SessionIds)
        VALUES
            (@Id, @Title, @Description, @Priority, @Status, @CreatedAt, @CompletedAt,
             @EstimatedPomodoros, @CompletedPomodoros, @SessionIds);
        """;

    public TaskItem Read(SqliteDataReader r)
    {
        return new TaskItem
        {
            Id = Guid.Parse(r.GetString("Id")),
            Title = r.GetString("Title"),
            Description = r.GetString("Description"),
            Priority = (Domain.Enums.TaskPriority)r.GetInt32("Priority"),
            Status = (Domain.Enums.TaskItemStatus)r.GetInt32("Status"),
            CreatedAt = DateTime.Parse(r.GetString("CreatedAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            CompletedAt = r.IsDBNull("CompletedAt")
                ? null
                : DateTime.Parse(r.GetString("CompletedAt"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            EstimatedPomodoros = r.GetInt32("EstimatedPomodoros"),
            CompletedPomodoros = r.GetInt32("CompletedPomodoros"),
            SessionIds = ParseSessionIds(r.GetString("SessionIds")),
        };
    }

    public void AddParameters(SqliteCommand cmd, TaskItem t)
    {
        cmd.Parameters.AddWithValue("@Id", t.Id.ToString());
        cmd.Parameters.AddWithValue("@Title", t.Title ?? string.Empty);
        cmd.Parameters.AddWithValue("@Description", t.Description ?? string.Empty);
        cmd.Parameters.AddWithValue("@Priority", (int)t.Priority);
        cmd.Parameters.AddWithValue("@Status", (int)t.Status);
        cmd.Parameters.AddWithValue("@CreatedAt", t.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)t.CompletedAt?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EstimatedPomodoros", t.EstimatedPomodoros);
        cmd.Parameters.AddWithValue("@CompletedPomodoros", t.CompletedPomodoros);
        cmd.Parameters.AddWithValue("@SessionIds", SerializeSessionIds(t.SessionIds));
    }

    private static string SerializeSessionIds(List<Guid> ids) =>
        JsonSerializer.Serialize(ids ?? new List<Guid>());

    private static List<Guid> ParseSessionIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<Guid>();
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? new List<Guid>();
        }
        catch
        {
            return new List<Guid>();
        }
    }
}

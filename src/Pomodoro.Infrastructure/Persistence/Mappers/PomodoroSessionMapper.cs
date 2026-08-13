using System.Globalization;
using Microsoft.Data.Sqlite;
using Pomodoro.Domain.Entities;

namespace Pomodoro.Infrastructure.Persistence.Mappers;

public sealed class PomodoroSessionMapper : ISqliteMapper<PomodoroSession>
{
    public string TableName => "sessions";
    public string PrimaryKeyColumn => "Id";
    public bool HasGuidPrimaryKey => true;

    public string CreateTableSql => """
        CREATE TABLE IF NOT EXISTS sessions (
            Id TEXT PRIMARY KEY NOT NULL,
            TaskId TEXT,
            Phase INTEGER NOT NULL,
            StartedAt TEXT NOT NULL,
            EndedAt TEXT,
            PlannedDurationSec INTEGER NOT NULL,
            ActualDurationSec INTEGER NOT NULL,
            WasCompleted INTEGER NOT NULL,
            AbandonReason TEXT,
            CycleIndex INTEGER NOT NULL,
            IsLongBreak INTEGER NOT NULL
        );
        """;

    public IReadOnlyList<string> CreateIndexSqls => new[]
    {
        "CREATE INDEX IF NOT EXISTS idx_sessions_started ON sessions(StartedAt);",
        "CREATE INDEX IF NOT EXISTS idx_sessions_task ON sessions(TaskId);",
        "CREATE INDEX IF NOT EXISTS idx_sessions_phase ON sessions(Phase);",
    };

    public string UpsertSql => """
        INSERT OR REPLACE INTO sessions
            (Id, TaskId, Phase, StartedAt, EndedAt, PlannedDurationSec,
             ActualDurationSec, WasCompleted, AbandonReason, CycleIndex, IsLongBreak)
        VALUES
            (@Id, @TaskId, @Phase, @StartedAt, @EndedAt, @PlannedDurationSec,
             @ActualDurationSec, @WasCompleted, @AbandonReason, @CycleIndex, @IsLongBreak);
        """;

    public PomodoroSession Read(SqliteDataReader r)
    {
        return new PomodoroSession
        {
            Id = Guid.Parse(r.GetString("Id")),
            TaskId = r.IsDBNull("TaskId") ? null : Guid.Parse(r.GetString("TaskId")),
            Phase = (Domain.Enums.SessionPhase)r.GetInt32("Phase"),
            StartedAt = DateTime.Parse(r.GetString("StartedAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            EndedAt = r.IsDBNull("EndedAt")
                ? null
                : DateTime.Parse(r.GetString("EndedAt"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            PlannedDurationSec = r.GetInt32("PlannedDurationSec"),
            ActualDurationSec = r.GetInt32("ActualDurationSec"),
            WasCompleted = r.GetInt32("WasCompleted") != 0,
            AbandonReason = r.IsDBNull("AbandonReason") ? null : r.GetString("AbandonReason"),
            CycleIndex = r.GetInt32("CycleIndex"),
            IsLongBreak = r.GetInt32("IsLongBreak") != 0,
        };
    }

    public void AddParameters(SqliteCommand cmd, PomodoroSession s)
    {
        cmd.Parameters.AddWithValue("@Id", s.Id.ToString());
        cmd.Parameters.AddWithValue("@TaskId", (object?)s.TaskId?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Phase", (int)s.Phase);
        cmd.Parameters.AddWithValue("@StartedAt", s.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@EndedAt", (object?)s.EndedAt?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PlannedDurationSec", s.PlannedDurationSec);
        cmd.Parameters.AddWithValue("@ActualDurationSec", s.ActualDurationSec);
        cmd.Parameters.AddWithValue("@WasCompleted", s.WasCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@AbandonReason", (object?)s.AbandonReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CycleIndex", s.CycleIndex);
        cmd.Parameters.AddWithValue("@IsLongBreak", s.IsLongBreak ? 1 : 0);
    }
}

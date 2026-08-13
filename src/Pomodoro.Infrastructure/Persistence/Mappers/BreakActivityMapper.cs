using System.Globalization;
using Microsoft.Data.Sqlite;
using Pomodoro.Domain.Entities;

namespace Pomodoro.Infrastructure.Persistence.Mappers;

public sealed class BreakActivityMapper : ISqliteMapper<BreakActivity>
{
    public string TableName => "activities";
    public string PrimaryKeyColumn => "Id";
    public bool HasGuidPrimaryKey => true;

    public string CreateTableSql => """
        CREATE TABLE IF NOT EXISTS activities (
            Id TEXT PRIMARY KEY NOT NULL,
            BreakSessionId TEXT NOT NULL,
            CapturedAt TEXT NOT NULL,
            KeyPressCount INTEGER NOT NULL,
            MouseClickCount INTEGER NOT NULL,
            MouseDistancePx INTEGER NOT NULL,
            IdleSeconds INTEGER NOT NULL
        );
        """;

    public IReadOnlyList<string> CreateIndexSqls => new[]
    {
        "CREATE INDEX IF NOT EXISTS idx_activities_break ON activities(BreakSessionId);",
        "CREATE INDEX IF NOT EXISTS idx_activities_captured ON activities(CapturedAt);",
    };

    public string UpsertSql => """
        INSERT OR REPLACE INTO activities
            (Id, BreakSessionId, CapturedAt, KeyPressCount, MouseClickCount,
             MouseDistancePx, IdleSeconds)
        VALUES
            (@Id, @BreakSessionId, @CapturedAt, @KeyPressCount, @MouseClickCount,
             @MouseDistancePx, @IdleSeconds);
        """;

    public BreakActivity Read(SqliteDataReader r)
    {
        return new BreakActivity
        {
            Id = Guid.Parse(r.GetString("Id")),
            BreakSessionId = Guid.Parse(r.GetString("BreakSessionId")),
            CapturedAt = DateTime.Parse(r.GetString("CapturedAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            KeyPressCount = r.GetInt32("KeyPressCount"),
            MouseClickCount = r.GetInt32("MouseClickCount"),
            MouseDistancePx = r.GetInt32("MouseDistancePx"),
            IdleSeconds = r.GetInt32("IdleSeconds"),
        };
    }

    public void AddParameters(SqliteCommand cmd, BreakActivity a)
    {
        cmd.Parameters.AddWithValue("@Id", a.Id.ToString());
        cmd.Parameters.AddWithValue("@BreakSessionId", a.BreakSessionId.ToString());
        cmd.Parameters.AddWithValue("@CapturedAt", a.CapturedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@KeyPressCount", a.KeyPressCount);
        cmd.Parameters.AddWithValue("@MouseClickCount", a.MouseClickCount);
        cmd.Parameters.AddWithValue("@MouseDistancePx", a.MouseDistancePx);
        cmd.Parameters.AddWithValue("@IdleSeconds", a.IdleSeconds);
    }
}

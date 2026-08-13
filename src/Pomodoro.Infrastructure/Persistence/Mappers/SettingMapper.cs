using System.Globalization;
using Microsoft.Data.Sqlite;
using Pomodoro.Domain.Entities;

namespace Pomodoro.Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps Setting to/from the "settings" SQLite table.
/// Primary key is the string Key (not a Guid), so HasGuidPrimaryKey = false.
/// GetByIdAsync(Guid) returns null for Setting (SettingsService uses FindAsync instead).
/// </summary>
public sealed class SettingMapper : ISqliteMapper<Setting>
{
    public string TableName => "settings";
    public string PrimaryKeyColumn => "Key";
    public bool HasGuidPrimaryKey => false;

    public string CreateTableSql => """
        CREATE TABLE IF NOT EXISTS settings (
            Key TEXT PRIMARY KEY NOT NULL,
            Value TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );
        """;

    public IReadOnlyList<string> CreateIndexSqls => Array.Empty<string>();

    public string UpsertSql => """
        INSERT OR REPLACE INTO settings (Key, Value, UpdatedAt)
        VALUES (@Key, @Value, @UpdatedAt);
        """;

    public Setting Read(SqliteDataReader r)
    {
        return new Setting
        {
            Key = r.GetString("Key"),
            Value = r.GetString("Value"),
            UpdatedAt = DateTime.Parse(r.GetString("UpdatedAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        };
    }

    public void AddParameters(SqliteCommand cmd, Setting s)
    {
        cmd.Parameters.AddWithValue("@Key", s.Key ?? string.Empty);
        cmd.Parameters.AddWithValue("@Value", s.Value ?? string.Empty);
        cmd.Parameters.AddWithValue("@UpdatedAt", s.UpdatedAt.ToString("O"));
    }
}

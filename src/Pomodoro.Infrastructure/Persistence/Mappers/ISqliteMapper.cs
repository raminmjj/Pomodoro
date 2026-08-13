using Microsoft.Data.Sqlite;

namespace Pomodoro.Infrastructure.Persistence;

/// <summary>
/// AOT-safe mapper between an entity and a SQLite row.
/// Each entity has its own concrete implementation — no reflection.
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface ISqliteMapper<T> where T : class
{
    /// <summary>SQLite table name (e.g. "tasks", "sessions").</summary>
    string TableName { get; }

    /// <summary>Primary key column name. "Id" for most entities, "Key" for Setting.</summary>
    string PrimaryKeyColumn { get; }

    /// <summary>True if PK is a Guid (so GetByIdAsync(Guid) makes sense). False for Setting (PK = string).</summary>
    bool HasGuidPrimaryKey { get; }

    /// <summary>Full CREATE TABLE IF NOT EXISTS statement.</summary>
    string CreateTableSql { get; }

    /// <summary>CREATE INDEX statements (may be empty).</summary>
    IReadOnlyList<string> CreateIndexSqls { get; }

    /// <summary>INSERT OR REPLACE INTO statement with @parameters matching AddParameters.</summary>
    string UpsertSql { get; }

    /// <summary>Read one row from the reader (caller advances to next row before calling).</summary>
    T Read(SqliteDataReader reader);

    /// <summary>Populate @parameters on the command from the entity.</summary>
    void AddParameters(SqliteCommand cmd, T entity);
}

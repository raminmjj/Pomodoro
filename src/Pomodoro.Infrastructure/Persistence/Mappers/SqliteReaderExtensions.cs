using Microsoft.Data.Sqlite;

namespace Pomodoro.Infrastructure.Persistence.Mappers;

/// <summary>
/// Extension helpers for SqliteDataReader.
/// Microsoft.Data.Sqlite only accepts int ordinals, not column names —
/// these helpers wrap GetOrdinal + GetXxx.
/// </summary>
internal static class SqliteReaderExtensions
{
    public static string GetString(this SqliteDataReader r, string name) =>
        r.GetString(r.GetOrdinal(name));

    public static int GetInt32(this SqliteDataReader r, string name) =>
        r.GetInt32(r.GetOrdinal(name));

    public static long GetInt64(this SqliteDataReader r, string name) =>
        r.GetInt64(r.GetOrdinal(name));

    public static bool IsDBNull(this SqliteDataReader r, string name) =>
        r.IsDBNull(r.GetOrdinal(name));
}

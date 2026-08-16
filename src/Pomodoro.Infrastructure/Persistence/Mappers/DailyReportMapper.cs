using System.Globalization;
using Microsoft.Data.Sqlite;
using Pomodoro.Domain.Entities;

namespace Pomodoro.Infrastructure.Persistence.Mappers;

public sealed class DailyReportMapper : ISqliteMapper<DailyReport>
{
    public string TableName => "reports";
    public string PrimaryKeyColumn => "Id";
    public bool HasGuidPrimaryKey => true;

    public string CreateTableSql => """
        CREATE TABLE IF NOT EXISTS reports (
            Id TEXT PRIMARY KEY NOT NULL,
            Date TEXT NOT NULL UNIQUE,
            CompletedFocusSessions INTEGER NOT NULL,
            TotalFocusSeconds INTEGER NOT NULL,
            TotalBreakSeconds INTEGER NOT NULL,
            TotalKeystrokes INTEGER NOT NULL,
            TotalMouseClicks INTEGER NOT NULL,
            TotalIdleSeconds INTEGER NOT NULL,
            TaskBreakdownJson TEXT NOT NULL,
            HourlyKeystrokesJson TEXT NOT NULL,
            HourlyMouseClicksJson TEXT NOT NULL,
            HourlyFocusMinutesJson TEXT NOT NULL,
            HourlyBreakMinutesJson TEXT NOT NULL,
            GeneratedAt TEXT NOT NULL
        );
        """;

    public IReadOnlyList<string> CreateIndexSqls => new[]
    {
        "CREATE INDEX IF NOT EXISTS idx_reports_date ON reports(Date);",
    };

    public IReadOnlyList<string> MigrateSqls => new[]
    {
        "ALTER TABLE reports ADD COLUMN HourlyFocusMinutesJson TEXT NOT NULL DEFAULT '[]';",
        "ALTER TABLE reports ADD COLUMN HourlyBreakMinutesJson TEXT NOT NULL DEFAULT '[]';",
    };

    public string UpsertSql => """
        INSERT OR REPLACE INTO reports
            (Id, Date, CompletedFocusSessions, TotalFocusSeconds, TotalBreakSeconds,
             TotalKeystrokes, TotalMouseClicks, TotalIdleSeconds,
             TaskBreakdownJson, HourlyKeystrokesJson, HourlyMouseClicksJson,
             HourlyFocusMinutesJson, HourlyBreakMinutesJson, GeneratedAt)
        VALUES
            (@Id, @Date, @CompletedFocusSessions, @TotalFocusSeconds, @TotalBreakSeconds,
             @TotalKeystrokes, @TotalMouseClicks, @TotalIdleSeconds,
             @TaskBreakdownJson, @HourlyKeystrokesJson, @HourlyMouseClicksJson,
             @HourlyFocusMinutesJson, @HourlyBreakMinutesJson, @GeneratedAt);
        """;

    public DailyReport Read(SqliteDataReader r)
    {
        return new DailyReport
        {
            Id = Guid.Parse(r.GetString("Id")),
            Date = DateTime.Parse(r.GetString("Date"), CultureInfo.InvariantCulture,
                DateTimeStyles.None),
            CompletedFocusSessions = r.GetInt32("CompletedFocusSessions"),
            TotalFocusSeconds = r.GetInt32("TotalFocusSeconds"),
            TotalBreakSeconds = r.GetInt32("TotalBreakSeconds"),
            TotalKeystrokes = r.GetInt32("TotalKeystrokes"),
            TotalMouseClicks = r.GetInt32("TotalMouseClicks"),
            TotalIdleSeconds = r.GetInt32("TotalIdleSeconds"),
            TaskBreakdownJson = r.GetString("TaskBreakdownJson"),
            HourlyKeystrokesJson = r.GetString("HourlyKeystrokesJson"),
            HourlyMouseClicksJson = r.GetString("HourlyMouseClicksJson"),
            HourlyFocusMinutesJson = r.GetString("HourlyFocusMinutesJson"),
            HourlyBreakMinutesJson = r.GetString("HourlyBreakMinutesJson"),
            GeneratedAt = DateTime.Parse(r.GetString("GeneratedAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        };
    }

    public void AddParameters(SqliteCommand cmd, DailyReport rep)
    {
        cmd.Parameters.AddWithValue("@Id", rep.Id.ToString());
        cmd.Parameters.AddWithValue("@Date", rep.Date.ToString("O"));
        cmd.Parameters.AddWithValue("@CompletedFocusSessions", rep.CompletedFocusSessions);
        cmd.Parameters.AddWithValue("@TotalFocusSeconds", rep.TotalFocusSeconds);
        cmd.Parameters.AddWithValue("@TotalBreakSeconds", rep.TotalBreakSeconds);
        cmd.Parameters.AddWithValue("@TotalKeystrokes", rep.TotalKeystrokes);
        cmd.Parameters.AddWithValue("@TotalMouseClicks", rep.TotalMouseClicks);
        cmd.Parameters.AddWithValue("@TotalIdleSeconds", rep.TotalIdleSeconds);
        cmd.Parameters.AddWithValue("@TaskBreakdownJson", rep.TaskBreakdownJson ?? "[]");
        cmd.Parameters.AddWithValue("@HourlyKeystrokesJson", rep.HourlyKeystrokesJson ?? "[]");
        cmd.Parameters.AddWithValue("@HourlyMouseClicksJson", rep.HourlyMouseClicksJson ?? "[]");
        cmd.Parameters.AddWithValue("@HourlyFocusMinutesJson", rep.HourlyFocusMinutesJson ?? "[]");
        cmd.Parameters.AddWithValue("@HourlyBreakMinutesJson", rep.HourlyBreakMinutesJson ?? "[]");
        cmd.Parameters.AddWithValue("@GeneratedAt", rep.GeneratedAt.ToString("O"));
    }
}

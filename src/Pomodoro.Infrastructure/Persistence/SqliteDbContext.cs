using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Pomodoro.Infrastructure.Persistence;

/// <summary>
/// Owns the SqliteConnection and ensures schema exists.
/// AOT-safe: no reflection — uses explicit ISqliteMapper&lt;T&gt; implementations.
/// </summary>
public sealed class SqliteDbContext : IDisposable, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<SqliteDbContext> _logger;
    private int _initialized;

    public SqliteConnection Connection => _connection;

    // Mappers (resolved via DI as singletons)
    public ISqliteMapper<Domain.Entities.TaskItem> TasksMapper { get; }
    public ISqliteMapper<Domain.Entities.PomodoroSession> SessionsMapper { get; }
    public ISqliteMapper<Domain.Entities.BreakActivity> ActivitiesMapper { get; }
    public ISqliteMapper<Domain.Entities.Setting> SettingsMapper { get; }
    public ISqliteMapper<Domain.Entities.DailyReport> ReportsMapper { get; }

    public SqliteDbContext(
        string dbPath,
        ISqliteMapper<Domain.Entities.TaskItem> tasksMapper,
        ISqliteMapper<Domain.Entities.PomodoroSession> sessionsMapper,
        ISqliteMapper<Domain.Entities.BreakActivity> activitiesMapper,
        ISqliteMapper<Domain.Entities.Setting> settingsMapper,
        ISqliteMapper<Domain.Entities.DailyReport> reportsMapper,
        ILogger<SqliteDbContext> logger)
    {
        TasksMapper = tasksMapper;
        SessionsMapper = sessionsMapper;
        ActivitiesMapper = activitiesMapper;
        SettingsMapper = settingsMapper;
        ReportsMapper = reportsMapper;
        _logger = logger;

        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        _connection = new SqliteConnection(connStr);
        _connection.Open();
    }

    /// <summary>Idempotent: creates tables and indexes if they don't exist.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1) return;

        await using var tx = (SqliteTransaction)await _connection.BeginTransactionAsync(ct);

        await InitOneAsync(TasksMapper, tx, ct);
        await InitOneAsync(SessionsMapper, tx, ct);
        await InitOneAsync(ActivitiesMapper, tx, ct);
        await InitOneAsync(SettingsMapper, tx, ct);
        await InitOneAsync(ReportsMapper, tx, ct);

        await tx.CommitAsync(ct);
        _logger.LogInformation("SQLite schema initialized");
    }

    private async Task InitOneAsync<T>(ISqliteMapper<T> mapper, SqliteTransaction tx, CancellationToken ct)
        where T : class
    {
        await ExecAsync(mapper.CreateTableSql, tx, ct);
        foreach (var idx in mapper.CreateIndexSqls)
            await ExecAsync(idx, tx, ct);
    }

    private async Task ExecAsync(string sql, SqliteTransaction tx, CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _logger.LogInformation("SQLite closed");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}

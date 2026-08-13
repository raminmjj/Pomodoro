using System.Linq.Expressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Infrastructure.Persistence;

/// <summary>
/// Generic SQLite-backed repository. AOT-safe: uses explicit ISqliteMapper&lt;T&gt;
/// implementations instead of reflection.
///
/// Strategy:
///   - GetByIdAsync: SELECT WHERE PK = @id (only for entities with Guid PK)
///   - GetAllAsync: SELECT * — manual deserialize via mapper
///   - FindAsync: GetAllAsync + in-memory predicate (data volume is small)
///   - UpsertAsync: INSERT OR REPLACE
///   - DeleteAsync: DELETE WHERE PK = @id
///   - CountAsync: SELECT COUNT(*) if no predicate, else in-memory
/// </summary>
public sealed class SqliteRepository<T> : IRepository<T> where T : class
{
    private readonly SqliteDbContext _ctx;
    private readonly ISqliteMapper<T> _mapper;
    private readonly ILogger<SqliteRepository<T>> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteRepository(
        SqliteDbContext ctx,
        ISqliteMapper<T> mapper,
        ILogger<SqliteRepository<T>> logger)
    {
        _ctx = ctx;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (!_mapper.HasGuidPrimaryKey)
        {
            // Setting has a string PK; GetByIdAsync(Guid) is never called for it.
            return null;
        }

        await _lock.WaitAsync(ct);
        try
        {
            await using var cmd = _ctx.Connection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {_mapper.TableName} WHERE {_mapper.PrimaryKeyColumn} = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", id.ToString());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            return _mapper.Read(r);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var result = new List<T>();
            await using var cmd = _ctx.Connection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {_mapper.TableName};";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                result.Add(_mapper.Read(r));
            }
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        // AOT-safe: compile expression to a delegate and filter in memory.
        // For Pomodoro data volumes (at most a few thousand rows), this is fast enough.
        var compiled = predicate.Compile();
        var all = await GetAllAsync(ct);
        return all.Where(compiled).ToList();
    }

    public async Task UpsertAsync(T entity, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var cmd = _ctx.Connection.CreateCommand();
            cmd.CommandText = _mapper.UpsertSql;
            _mapper.AddParameters(cmd, entity);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!_mapper.HasGuidPrimaryKey)
        {
            // Setting: not used by the app (SettingsService doesn't delete rows).
            return;
        }

        await _lock.WaitAsync(ct);
        try
        {
            await using var cmd = _ctx.Connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM {_mapper.TableName} WHERE {_mapper.PrimaryKeyColumn} = @id;";
            cmd.Parameters.AddWithValue("@id", id.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        if (predicate is null)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await using var cmd = _ctx.Connection.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM {_mapper.TableName};";
                var result = await cmd.ExecuteScalarAsync(ct);
                return Convert.ToInt32(result);
            }
            finally
            {
                _lock.Release();
            }
        }
        else
        {
            var compiled = predicate.Compile();
            var all = await GetAllAsync(ct);
            return all.Count(compiled);
        }
    }
}

using System.Linq.Expressions;
using LiteDB;
using Pomodoro.Domain.Interfaces;

namespace Pomodoro.Infrastructure.Persistence;

/// <summary>
/// Generic LiteDB-backed repository. AOT-safe because it uses ILiteCollection
/// directly without reflection.
/// </summary>
public sealed class LiteRepository<T> : IRepository<T> where T : class
{
    private readonly ILiteCollection<T> _collection;

    public LiteRepository(ILiteCollection<T> collection)
    {
        _collection = collection;
    }

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_collection.FindById(id));
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<T>>(_collection.FindAll().ToList());
    }

    public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<T>>(_collection.Find(predicate).ToList());
    }

    public Task UpsertAsync(T entity, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _collection.Upsert(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _collection.Delete(id);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(predicate is null
            ? _collection.Count()
            : _collection.Count(predicate));
    }
}

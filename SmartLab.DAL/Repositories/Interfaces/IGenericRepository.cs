using System.Linq.Expressions;

namespace SmartLab.DAL.Repositories.Interfaces;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);

    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    /// <summary>Tracked query for composing Include/Where/Select/paging in services.</summary>
    IQueryable<T> Query();

    /// <summary>Untracked query for read-only scenarios.</summary>
    IQueryable<T> QueryNoTracking();

    Task AddAsync(T entity, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    void Update(T entity);

    void Remove(T entity);
}

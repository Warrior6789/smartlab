using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartLab.DAL.Context;
using SmartLab.DAL.Repositories.Interfaces;

namespace SmartLab.DAL.Repositories.Implementations;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public GenericRepository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => await DbSet.FindAsync(new[] { id }, ct);

    public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => DbSet.Where(predicate).ToListAsync(ct);

    public IQueryable<T> Query() => DbSet;

    public IQueryable<T> QueryNoTracking() => DbSet.AsNoTracking();

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await DbSet.AddAsync(entity, ct);

    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        => DbSet.AddRangeAsync(entities, ct);

    public void Update(T entity) => DbSet.Update(entity);

    public void Remove(T entity) => DbSet.Remove(entity);
}

using Microsoft.EntityFrameworkCore.Storage;
using SmartLab.DAL.Repositories.Interfaces;

namespace SmartLab.DAL.UnitOfWork;

public interface IUnitOfWork
{
    IGenericRepository<T> Repository<T>() where T : class;

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

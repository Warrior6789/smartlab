using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Public;
using SmartLab.BLL.Interfaces.Public;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Public;

public class PublicStatsService : IPublicStatsService
{
    private readonly IUnitOfWork _uow;

    public PublicStatsService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<PublicStatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        var consumableUnits = await _uow.Repository<ConsumableStock>().QueryNoTracking()
            .SumAsync(s => (int?)s.Quantity, ct) ?? 0;

        var availableItems = await _uow.Repository<ComponentItem>().QueryNoTracking()
            .CountAsync(i => i.Status == ComponentItemStatuses.Available, ct);

        var borrows = _uow.Repository<BorrowRequest>().QueryNoTracking();

        var activeBorrows = await borrows
            .CountAsync(b => b.Status == BorrowRequestStatuses.Borrowing || b.Status == BorrowRequestStatuses.Overdue, ct);

        var returned = borrows.Where(b => b.Status == BorrowRequestStatuses.Returned && b.ReturnedAt != null);
        var returnedCount = await returned.CountAsync(ct);
        var onTimeCount = await returned.CountAsync(b => b.ReturnedAt <= b.DueDate, ct);

        decimal? onTimeRate = null;
        if (returnedCount > 0)
            onTimeRate = Math.Round(onTimeCount * 100m / returnedCount, 1);

        return new PublicStatsResponse
        {
            AvailableUnits = consumableUnits + availableItems,
            ActiveBorrows = activeBorrows,
            ReturnedBorrows = returnedCount,
            OnTimeReturnRate = onTimeRate,
            GeneratedAt = DateTime.UtcNow,
        };
    }
}

using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class InventoryTransactionService : IInventoryTransactionService
{
    /// <summary>Vietnam has no DST, so a fixed UTC+7 offset is exact.</summary>
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;

    public InventoryTransactionService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public Task<PagedResult<InventoryTransactionResponse>> GetTransactionsAsync(
        InventoryTransactionQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var transactions = _uow.Repository<InventoryTransaction>().QueryNoTracking();

        if (query.ComponentId.HasValue)
            transactions = transactions.Where(t => t.ComponentId == query.ComponentId.Value);

        if (query.CabinetId.HasValue)
            transactions = transactions.Where(t => t.CabinetId == query.CabinetId.Value);

        if (query.ItemId.HasValue)
            transactions = transactions.Where(t => t.ItemId == query.ItemId.Value);

        if (query.ReceiptId.HasValue)
            transactions = transactions.Where(t => t.ReceiptId == query.ReceiptId.Value);

        if (query.CreatedBy.HasValue)
            transactions = transactions.Where(t => t.CreatedBy == query.CreatedBy.Value);

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim();
            transactions = transactions.Where(t => t.Type == type);
        }

        if (query.FromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(query.FromDate.Value.Date - VietnamUtcOffset, DateTimeKind.Utc);
            transactions = transactions.Where(t => t.CreatedAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var toExclusive = DateTime.SpecifyKind(query.ToDate.Value.Date.AddDays(1) - VietnamUtcOffset, DateTimeKind.Utc);
            transactions = transactions.Where(t => t.CreatedAt < toExclusive);
        }

        var result = transactions
            .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.TransactionId)
            .Select(t => new InventoryTransactionResponse
            {
                TransactionId = t.TransactionId,
                Type = t.Type,
                ComponentId = t.ComponentId,
                ComponentCode = t.Component.ComponentCode,
                ComponentName = t.Component.ComponentName,
                CabinetId = t.CabinetId,
                CabinetCode = t.Cabinet != null ? t.Cabinet.CabinetCode : null,
                ItemId = t.ItemId,
                SerialNumber = t.Item != null ? t.Item.SerialNumber : null,
                QuantityChange = t.QuantityChange,
                Reason = t.Reason,
                ReceiptId = t.ReceiptId,
                ReceiptCode = t.Receipt != null ? t.Receipt.ReceiptCode : null,
                CreatedBy = t.CreatedBy,
                CreatedByName = t.CreatedByNavigation.FullName,
                CreatedAt = t.CreatedAt,
            });

        return PagedResult<InventoryTransactionResponse>.CreateAsync(result, paging, ct);
    }
}

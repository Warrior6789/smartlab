using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class InventoryTransactionService : IInventoryTransactionService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public InventoryTransactionService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public Task<PagedResult<InventoryTransactionResponse>> GetTransactionsAsync(
        InventoryTransactionQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var transactions = _uow.Repository<InventoryTransaction>().QueryNoTracking();

        if (!_currentUser.HasPermission(PermissionCodes.InventoryAuditApprove))
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedException();
            transactions = transactions.Where(t => t.Cabinet != null && t.Cabinet.Room.LabRoomStaffs.Any(s => s.UserId == userId));
        }

        if (query.RoomId.HasValue)
            transactions = transactions.Where(t => t.Cabinet != null && t.Cabinet.RoomId == query.RoomId.Value);

        if (query.ComponentId.HasValue)
            transactions = transactions.Where(t => t.ComponentId == query.ComponentId.Value);

        if (query.CabinetId.HasValue)
            transactions = transactions.Where(t => t.CabinetId == query.CabinetId.Value);

        if (query.ItemId.HasValue)
            transactions = transactions.Where(t => t.ItemId == query.ItemId.Value);

        if (query.ReceiptId.HasValue)
            transactions = transactions.Where(t => t.ReceiptId == query.ReceiptId.Value);

        if (query.AuditId.HasValue)
            transactions = transactions.Where(t => t.AuditId == query.AuditId.Value);

        if (query.CreatedBy.HasValue)
            transactions = transactions.Where(t => t.CreatedBy == query.CreatedBy.Value);

        var type = InventoryTransactionTypes.Normalize(query.Type);
        if (type != null)
            transactions = transactions.Where(t => t.Type == type);

        if (query.FromDate.HasValue)
        {
            var from = VietnamDays.StartUtc(query.FromDate.Value);
            transactions = transactions.Where(t => t.CreatedAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var toExclusive = VietnamDays.EndExclusiveUtc(query.ToDate.Value);
            transactions = transactions.Where(t => t.CreatedAt < toExclusive);
        }

        var result = ToResponses(transactions.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.TransactionId));

        return PagedResult<InventoryTransactionResponse>.CreateAsync(result, paging, ct);
    }

    internal static IQueryable<InventoryTransactionResponse> ToResponses(IQueryable<InventoryTransaction> transactions) =>
        transactions.Select(t => new InventoryTransactionResponse
        {
            TransactionId = t.TransactionId,
            Type = t.Type,
            ComponentId = t.ComponentId,
            ComponentCode = t.Component.ShortCode ?? t.Component.ComponentCode,
            ComponentName = t.Component.ComponentName,
            CabinetId = t.CabinetId,
            CabinetCode = t.Cabinet != null ? t.Cabinet.CabinetCode : null,
            RoomCode = t.Cabinet != null ? t.Cabinet.Room.RoomCode : null,
            ItemId = t.ItemId,
            ItemCode = t.Item != null ? t.Item.ItemCode : null,
            SerialNumber = t.Item != null ? t.Item.SerialNumber : null,
            QuantityChange = t.QuantityChange,
            Reason = t.Reason,
            ReceiptId = t.ReceiptId,
            ReceiptCode = t.Receipt != null ? t.Receipt.ReceiptCode : null,
            AuditId = t.AuditId,
            CreatedBy = t.CreatedBy,
            CreatedByName = t.CreatedByNavigation.FullName,
            CreatedAt = t.CreatedAt,
        });
}

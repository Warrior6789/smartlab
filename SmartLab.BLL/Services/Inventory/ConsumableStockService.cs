using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class ConsumableStockService : IConsumableStockService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ConsumableStockService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public Task<PagedResult<ConsumableStockResponse>> GetStocksAsync(
        ConsumableStockQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var stocks = _uow.Repository<ConsumableStock>().QueryNoTracking();

        if (!query.IncludeEmpty)
            stocks = stocks.Where(st => st.Quantity > 0);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            stocks = stocks.Where(st => st.Component.ComponentCode.ToLower().Contains(keyword)
                                     || (st.Component.ShortCode != null && st.Component.ShortCode.ToLower().Contains(keyword))
                                     || st.Component.ComponentName.ToLower().Contains(keyword));
        }

        if (query.ComponentId.HasValue)
            stocks = stocks.Where(st => st.ComponentId == query.ComponentId.Value);

        if (query.CabinetId.HasValue)
            stocks = stocks.Where(st => st.CabinetId == query.CabinetId.Value);

        if (query.RoomId.HasValue)
            stocks = stocks.Where(st => st.Cabinet.RoomId == query.RoomId.Value);

        var result = ToResponses(stocks
            .OrderBy(st => st.Cabinet.CabinetCode).ThenBy(st => st.Component.ComponentCode)
            .ThenBy(st => st.CabinetId).ThenBy(st => st.ComponentId));

        return PagedResult<ConsumableStockResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<IReadOnlyList<ConsumableStockResponse>> MoveStockAsync(
        MoveConsumableStockRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        await RoomStock.EnsureMayMoveAsync(_uow, _currentUser, request.FromCabinetId, request.ToCabinetId,
            request.ReceiptId.HasValue, ct);

        await EnsureConsumableAsync(request.ComponentId, ct);

        if (!await _uow.Repository<StorageCabinet>().QueryNoTracking()
                .AnyAsync(c => c.CabinetId == request.ToCabinetId && c.Status == CabinetStatuses.Active, ct))
            throw new BadRequestException("Tủ đích không tồn tại hoặc đã ngừng hoạt động");

        await CabinetRules.EnsureAcceptsAsync(_uow, request.ToCabinetId, new[] { request.ComponentId }, ct);

        if (request.ReceiptId.HasValue)
            await StockReceiptService.EnsureReceiptHasComponentAsync(_uow, request.ReceiptId.Value, request.ComponentId, ct);

        string? reason = null;
        if (!string.IsNullOrWhiteSpace(request.Reason))
            reason = request.Reason.Trim();

        var stocks = _uow.Repository<ConsumableStock>();
        var now = DateTime.UtcNow;
        await using var tx = await _uow.BeginTransactionAsync(ct);

        var taken = await stocks.Query()
            .Where(st => st.ComponentId == request.ComponentId
                         && st.CabinetId == request.FromCabinetId
                         && st.Quantity >= request.Quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(st => st.Quantity, st => st.Quantity - request.Quantity)
                .SetProperty(st => st.UpdatedAt, now), ct);
        if (taken == 0)
        {
            var available = await stocks.QueryNoTracking()
                .Where(st => st.ComponentId == request.ComponentId && st.CabinetId == request.FromCabinetId)
                .Select(st => st.Quantity)
                .FirstOrDefaultAsync(ct);
            throw new BadRequestException($"Tủ nguồn chỉ còn {available}, không chuyển được {request.Quantity}");
        }

        var added = await stocks.Query()
            .Where(st => st.ComponentId == request.ComponentId && st.CabinetId == request.ToCabinetId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(st => st.Quantity, st => st.Quantity + request.Quantity)
                .SetProperty(st => st.UpdatedAt, now), ct);
        if (added == 0)
        {
            await stocks.AddAsync(new ConsumableStock
            {
                ComponentId = request.ComponentId,
                CabinetId = request.ToCabinetId,
                Quantity = request.Quantity,
                UpdatedAt = now,
            }, ct);
        }

        var transactions = _uow.Repository<InventoryTransaction>();
        await transactions.AddAsync(new InventoryTransaction
        {
            Type = InventoryTransactionTypes.Move,
            ComponentId = request.ComponentId,
            CabinetId = request.FromCabinetId,
            QuantityChange = -request.Quantity,
            Reason = reason,
            ReceiptId = request.ReceiptId,
            CreatedBy = userId,
            CreatedAt = now,
        }, ct);
        await transactions.AddAsync(new InventoryTransaction
        {
            Type = InventoryTransactionTypes.Move,
            ComponentId = request.ComponentId,
            CabinetId = request.ToCabinetId,
            QuantityChange = request.Quantity,
            Reason = reason,
            ReceiptId = request.ReceiptId,
            CreatedBy = userId,
            CreatedAt = now,
        }, ct);

        await SaveStockChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await ToResponses(stocks.QueryNoTracking()
                .Where(st => st.ComponentId == request.ComponentId
                             && (st.CabinetId == request.FromCabinetId || st.CabinetId == request.ToCabinetId))
                .OrderBy(st => st.Cabinet.CabinetCode))
            .ToListAsync(ct);
    }

    private async Task EnsureConsumableAsync(Guid componentId, CancellationToken ct)
    {
        var trackingType = await _uow.Repository<Component>().QueryNoTracking()
                               .Where(c => c.ComponentId == componentId)
                               .Select(c => c.TrackingType)
                               .FirstOrDefaultAsync(ct)
                           ?? throw new NotFoundException("Không tìm thấy linh kiện");

        if (trackingType != ComponentTrackingTypes.Consumable)
            throw new BadRequestException("Chỉ áp dụng cho linh kiện tiêu hao, linh kiện theo từng con hãy dùng chức năng chuyển/đổi trạng thái");
    }

    /// <summary>A unique violation here means another request created the same (component, cabinet) row at the same time.</summary>
    private async Task SaveStockChangesAsync(CancellationToken ct)
    {
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Tồn kho vừa thay đổi bởi thao tác khác, vui lòng thử lại");
        }
    }

    private static IQueryable<ConsumableStockResponse> ToResponses(IQueryable<ConsumableStock> stocks) =>
        stocks.Select(st => new ConsumableStockResponse
        {
            ComponentId = st.ComponentId,
            ComponentCode = st.Component.ShortCode ?? st.Component.ComponentCode,
            ComponentName = st.Component.ComponentName,
            CabinetId = st.CabinetId,
            CabinetCode = st.Cabinet.CabinetCode,
            CabinetName = st.Cabinet.CabinetName,
            RoomId = st.Cabinet.RoomId,
            RoomCode = st.Cabinet.Room.RoomCode,
            Quantity = st.Quantity,
            UpdatedAt = st.UpdatedAt,
        });
}

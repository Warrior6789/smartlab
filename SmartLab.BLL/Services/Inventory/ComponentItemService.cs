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

public class ComponentItemService : IComponentItemService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ComponentItemService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public Task<PagedResult<ComponentItemResponse>> GetItemsAsync(
        ComponentItemQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var items = _uow.Repository<ComponentItem>().QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            items = items.Where(i => i.ItemCode.ToLower().Contains(keyword)
                                     || (i.SerialNumber != null && i.SerialNumber.ToLower().Contains(keyword)));
        }

        if (query.ComponentId.HasValue)
            items = items.Where(i => i.ComponentId == query.ComponentId.Value);

        if (query.CabinetId.HasValue)
            items = items.Where(i => i.CabinetId == query.CabinetId.Value);

        var status = ComponentItemStatuses.Normalize(query.Status);
        if (status != null)
            items = items.Where(i => i.Status == status);

        if (query.WarrantyExpiringInDays.HasValue)
        {
            var today = DateTime.UtcNow.Date;
            var until = today.AddDays(query.WarrantyExpiringInDays.Value);
            items = items.Where(i => i.WarrantyExpiresAt != null
                                     && i.WarrantyExpiresAt >= today
                                     && i.WarrantyExpiresAt <= until
                                     && i.Status != ComponentItemStatuses.Lost
                                     && i.Status != ComponentItemStatuses.Retired);
        }

        var result = ToResponses(items.OrderBy(i => i.ItemCode));

        return PagedResult<ComponentItemResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<ComponentItemResponse> GetItemByIdAsync(Guid itemId, CancellationToken ct = default)
    {
        return await ToResponses(_uow.Repository<ComponentItem>().QueryNoTracking()
                       .Where(i => i.ItemId == itemId))
                   .FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("Không tìm thấy linh kiện");
    }

    public async Task<ComponentItemResponse> UpdateItemAsync(
        Guid itemId, UpdateComponentItemRequest request, CancellationToken ct = default)
    {
        var items = _uow.Repository<ComponentItem>();
        var item = await items.Query()
                       .Include(i => i.Component)
                       .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
                   ?? throw new NotFoundException("Không tìm thấy linh kiện");

        if (item.Component.TrackingType == ComponentTrackingTypes.HighValue)
        {
            if (string.IsNullOrWhiteSpace(request.SerialNumber))
                throw new BadRequestException("Thiết bị đắt tiền bắt buộc phải có serial");

            var serial = StockReceiptService.NormalizeSerial(request.SerialNumber);
            if (serial != item.SerialNumber
                && await items.QueryNoTracking().AnyAsync(i => i.ComponentId == item.ComponentId && i.SerialNumber == serial, ct))
                throw new ConflictException($"Serial {serial} đã có trong kho");

            item.SerialNumber = serial;
            item.HardwareVersion = string.IsNullOrWhiteSpace(request.HardwareVersion) ? null : request.HardwareVersion.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(request.SerialNumber) || !string.IsNullOrWhiteSpace(request.HardwareVersion))
        {
            throw new BadRequestException("Chỉ thiết bị đắt tiền mới có serial và version");
        }

        if (request.WarrantyExpiresAt.HasValue)
            item.WarrantyExpiresAt = DateTime.SpecifyKind(request.WarrantyExpiresAt.Value.Date, DateTimeKind.Utc);
        else
            item.WarrantyExpiresAt = null;

        item.Condition = string.IsNullOrWhiteSpace(request.Condition) ? null : request.Condition.Trim();
        item.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        item.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Serial vừa được dùng cho máy khác, vui lòng kiểm tra lại");
        }

        return await GetItemByIdAsync(itemId, ct);
    }

    public async Task<MoveComponentItemsResponse> MoveItemsAsync(MoveComponentItemsRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var toCabinetId = request.ToCabinetId ?? request.FromCabinetId;
        var toStatus = request.ToStatus ?? request.FromStatus;

        if (!await _uow.Repository<StorageCabinet>().QueryNoTracking()
                .AnyAsync(c => c.CabinetId == toCabinetId && c.Status == CabinetStatuses.Active, ct))
            throw new BadRequestException("Tủ đích không tồn tại hoặc đã ngừng hoạt động");

        if (request.ReceiptId.HasValue)
            await StockReceiptService.EnsureReceiptHasComponentAsync(_uow, request.ReceiptId.Value, request.ComponentId, ct);

        var candidates = _uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.ComponentId == request.ComponentId
                        && i.CabinetId == request.FromCabinetId
                        && i.Status == request.FromStatus);

        var statusChanges = toStatus != request.FromStatus;
        var skippedText = "";
        if (statusChanges)
        {
            candidates = candidates.Where(i => !i.IssueReports.Any(r => r.Status == IssueReportStatuses.Open));
            skippedText = " (không tính con đang có báo cáo sự cố chờ xử lý)";
        }

        List<Guid> itemIds;
        if (request.ItemIds != null)
        {
            var requestedIds = request.ItemIds;
            itemIds = await candidates
                .Where(i => requestedIds.Contains(i.ItemId))
                .OrderBy(i => i.ImportedAt).ThenBy(i => i.ItemId)
                .Select(i => i.ItemId)
                .ToListAsync(ct);
            if (itemIds.Count < requestedIds.Count)
                throw new BadRequestException(
                    $"Có {requestedIds.Count - itemIds.Count} con không thuộc linh kiện này, không nằm ở tủ nguồn, không ở trạng thái {request.FromStatus} hoặc đang có báo cáo sự cố chờ xử lý");
        }
        else
        {
            itemIds = await candidates
                .OrderBy(i => i.ImportedAt).ThenBy(i => i.ItemId)
                .Select(i => i.ItemId)
                .Take(request.Quantity)
                .ToListAsync(ct);
            if (itemIds.Count < request.Quantity)
                throw new BadRequestException($"Tủ chỉ còn {itemIds.Count} con ở trạng thái {request.FromStatus}{skippedText}");
        }

        var now = DateTime.UtcNow;
        await using var tx = await _uow.BeginTransactionAsync(ct);

        var fromAvailable = request.FromStatus == ComponentItemStatuses.Available;
        var sameRoom = await RoomStock.SameRoomAsync(_uow, request.FromCabinetId, toCabinetId, ct);
        if (fromAvailable && (!sameRoom || toStatus == ComponentItemStatuses.Maintenance))
            await RoomStock.EnsureNotReservedAsync(_uow,
                new[] { new StockRemoval(request.ComponentId, request.FromCabinetId, itemIds.Count) }, ct);

        var movable = _uow.Repository<ComponentItem>().Query()
            .Where(i => itemIds.Contains(i.ItemId)
                        && i.CabinetId == request.FromCabinetId
                        && i.Status == request.FromStatus);
        if (statusChanges)
            movable = movable.Where(i => !i.IssueReports.Any(r => r.Status == IssueReportStatuses.Open));

        var moved = await movable
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.CabinetId, toCabinetId)
                .SetProperty(i => i.Status, toStatus)
                .SetProperty(i => i.UpdatedAt, now), ct);
        if (moved != itemIds.Count)
            throw new ConflictException("Số lượng vừa bị thay đổi bởi thao tác khác, vui lòng thử lại");

        string? reason = null;
        if (toStatus != request.FromStatus)
            reason = $"{request.FromStatus} -> {toStatus}";
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            if (reason == null)
                reason = request.Reason.Trim();
            else
                reason = $"{reason}: {request.Reason.Trim()}";
        }
        var backInStock = request.FromStatus == ComponentItemStatuses.Lost
                          || request.FromStatus == ComponentItemStatuses.Retired;

        var transactions = _uow.Repository<InventoryTransaction>();
        foreach (var itemId in itemIds)
        {
            if (backInStock)
            {
                await transactions.AddAsync(NewMoveTransaction(request.ComponentId, toCabinetId, itemId, 1, reason, request.ReceiptId, userId, now), ct);
            }
            else if (toCabinetId != request.FromCabinetId)
            {
                await transactions.AddAsync(NewMoveTransaction(request.ComponentId, request.FromCabinetId, itemId, -1, reason, request.ReceiptId, userId, now), ct);
                await transactions.AddAsync(NewMoveTransaction(request.ComponentId, toCabinetId, itemId, 1, reason, request.ReceiptId, userId, now), ct);
            }
            else
            {
                await transactions.AddAsync(NewMoveTransaction(request.ComponentId, toCabinetId, itemId, 0, reason, request.ReceiptId, userId, now), ct);
            }
        }
        await _uow.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        var response = new MoveComponentItemsResponse
        {
            Items = await ToResponses(_uow.Repository<ComponentItem>().QueryNoTracking()
                    .Where(i => itemIds.Contains(i.ItemId))
                    .OrderBy(i => i.ImportedAt).ThenBy(i => i.ItemId))
                .ToListAsync(ct),
        };

        if (fromAvailable && toStatus == ComponentItemStatuses.Broken)
        {
            var roomId = await RoomStock.GetRoomIdAsync(_uow, request.FromCabinetId, ct);
            response.ReservationShortages = await RoomStock.GetShortagesAsync(_uow, roomId, new[] { request.ComponentId }, ct);
        }

        return response;
    }

    public async Task<RetireComponentItemResponse> RetireItemAsync(
        Guid itemId, RetireComponentItemRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var item = await _uow.Repository<ComponentItem>().QueryNoTracking()
                       .Where(i => i.ItemId == itemId)
                       .Select(i => new { i.ComponentId, i.CabinetId, i.Status })
                       .FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Không tìm thấy linh kiện");

        if (item.Status == ComponentItemStatuses.Borrowed)
            throw new BadRequestException("Linh kiện đang được mượn, phải trả về trước khi loại khỏi kho");
        if (item.Status == ComponentItemStatuses.Lost || item.Status == ComponentItemStatuses.Retired)
            throw new ConflictException($"Linh kiện đã ở trạng thái {item.Status}");
        if (await _uow.Repository<IssueReport>().QueryNoTracking()
                .AnyAsync(r => r.ComponentItemId == itemId && r.Status == IssueReportStatuses.Open, ct))
            throw new ConflictException("Linh kiện đang có báo cáo sự cố chờ xử lý, hãy xử lý báo cáo trước");

        if (request.ReceiptId.HasValue)
            await StockReceiptService.EnsureReceiptHasComponentAsync(_uow, request.ReceiptId.Value, item.ComponentId, ct);

        var fromAvailable = item.Status == ComponentItemStatuses.Available;
        var now = DateTime.UtcNow;
        await using var tx = await _uow.BeginTransactionAsync(ct);

        if (fromAvailable && request.Status == ComponentItemStatuses.Retired)
            await RoomStock.EnsureNotReservedAsync(_uow,
                new[] { new StockRemoval(item.ComponentId, item.CabinetId, 1) }, ct);

        var updated = await _uow.Repository<ComponentItem>().Query()
            .Where(i => i.ItemId == itemId
                        && i.Status == item.Status
                        && !i.IssueReports.Any(r => r.Status == IssueReportStatuses.Open))
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, request.Status)
                .SetProperty(i => i.UpdatedAt, now), ct);
        if (updated == 0)
            throw new ConflictException("Linh kiện vừa bị thay đổi bởi thao tác khác, vui lòng thử lại");

        await _uow.Repository<InventoryTransaction>().AddAsync(new InventoryTransaction
        {
            Type = InventoryTransactionTypes.Retire,
            ComponentId = item.ComponentId,
            CabinetId = item.CabinetId,
            ItemId = itemId,
            QuantityChange = -1,
            Reason = $"{request.Status}: {request.Reason.Trim()}",
            ReceiptId = request.ReceiptId,
            CreatedBy = userId,
            CreatedAt = now,
        }, ct);
        await _uow.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        var response = new RetireComponentItemResponse { Item = await GetItemByIdAsync(itemId, ct) };

        if (fromAvailable && request.Status == ComponentItemStatuses.Lost)
        {
            var roomId = await RoomStock.GetRoomIdAsync(_uow, item.CabinetId, ct);
            response.ReservationShortages = await RoomStock.GetShortagesAsync(_uow, roomId, new[] { item.ComponentId }, ct);
        }

        return response;
    }

    private static InventoryTransaction NewMoveTransaction(
        Guid componentId, Guid cabinetId, Guid itemId, int quantityChange, string? reason, Guid? receiptId, Guid userId, DateTime now)
    {
        return new InventoryTransaction
        {
            Type = InventoryTransactionTypes.Move,
            ComponentId = componentId,
            CabinetId = cabinetId,
            ItemId = itemId,
            QuantityChange = quantityChange,
            Reason = reason,
            ReceiptId = receiptId,
            CreatedBy = userId,
            CreatedAt = now,
        };
    }

    private static IQueryable<ComponentItemResponse> ToResponses(IQueryable<ComponentItem> items) =>
        items.Select(i => new ComponentItemResponse
        {
            ItemId = i.ItemId,
            ItemCode = i.ItemCode,
            SerialNumber = i.SerialNumber,
            ComponentId = i.ComponentId,
            ComponentCode = i.Component.ComponentCode,
            ComponentName = i.Component.ComponentName,
            CabinetId = i.CabinetId,
            CabinetCode = i.Cabinet.CabinetCode,
            Status = i.Status,
            Condition = i.Condition,
            PurchasePrice = i.PurchasePrice,
            HardwareVersion = i.HardwareVersion,
            WarrantyExpiresAt = i.WarrantyExpiresAt,
            Note = i.Note,
            ImportedAt = i.ImportedAt,
            UpdatedAt = i.UpdatedAt,
        });
}

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

public class InventoryAuditService : IInventoryAuditService
{
    private static readonly string[] InCabinetStatuses =
    {
        ComponentItemStatuses.Available, ComponentItemStatuses.Broken, ComponentItemStatuses.Maintenance,
    };

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public InventoryAuditService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public Task<PagedResult<InventoryAuditResponse>> GetAuditsAsync(
        InventoryAuditQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var audits = _uow.Repository<InventoryAudit>().QueryNoTracking();

        if (query.CabinetId.HasValue)
            audits = audits.Where(a => a.CabinetId == query.CabinetId.Value);

        if (query.RoomId.HasValue)
            audits = audits.Where(a => a.Cabinet.RoomId == query.RoomId.Value);

        var status = InventoryAuditStatuses.Normalize(query.Status);
        if (status != null)
            audits = audits.Where(a => a.Status == status);

        var result = audits
            .OrderByDescending(a => a.AuditDate).ThenBy(a => a.AuditId)
            .Select(a => new InventoryAuditResponse
            {
                AuditId = a.AuditId,
                CabinetId = a.CabinetId,
                CabinetCode = a.Cabinet.CabinetCode,
                CabinetName = a.Cabinet.CabinetName,
                RoomId = a.Cabinet.RoomId,
                RoomCode = a.Cabinet.Room.RoomCode,
                AuditorId = a.AuditorId,
                AuditorName = a.Auditor.FullName,
                AuditDate = a.AuditDate,
                Status = a.Status,
                Note = a.Note,
                DifferenceCount = a.InventoryTransactions.Count,
            });

        return PagedResult<InventoryAuditResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<InventoryAuditDetailResponse> GetAuditByIdAsync(Guid auditId, CancellationToken ct = default)
    {
        var audit = await _uow.Repository<InventoryAudit>().QueryNoTracking()
                        .Where(a => a.AuditId == auditId)
                        .Select(a => new InventoryAuditDetailResponse
                        {
                            AuditId = a.AuditId,
                            CabinetId = a.CabinetId,
                            CabinetCode = a.Cabinet.CabinetCode,
                            CabinetName = a.Cabinet.CabinetName,
                            RoomId = a.Cabinet.RoomId,
                            RoomCode = a.Cabinet.Room.RoomCode,
                            AuditorId = a.AuditorId,
                            AuditorName = a.Auditor.FullName,
                            AuditDate = a.AuditDate,
                            Status = a.Status,
                            Note = a.Note,
                            DifferenceCount = a.InventoryTransactions.Count,
                        })
                        .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException("Không tìm thấy phiên kiểm kê");

        if (audit.Status == InventoryAuditStatuses.InProgress)
        {
            audit.Consumables = await _uow.Repository<ConsumableStock>().QueryNoTracking()
                .Where(st => st.CabinetId == audit.CabinetId && st.Quantity > 0)
                .OrderBy(st => st.Component.ComponentCode)
                .Select(st => new AuditConsumableLineResponse
                {
                    ComponentId = st.ComponentId,
                    ComponentCode = st.Component.ComponentCode,
                    ComponentName = st.Component.ComponentName,
                    SystemQuantity = st.Quantity,
                })
                .ToListAsync(ct);

            audit.Items = await _uow.Repository<ComponentItem>().QueryNoTracking()
                .Where(i => i.CabinetId == audit.CabinetId && InCabinetStatuses.Contains(i.Status))
                .OrderBy(i => i.Component.ComponentCode).ThenBy(i => i.ItemCode)
                .Select(i => new AuditItemLineResponse
                {
                    ItemId = i.ItemId,
                    ItemCode = i.ItemCode,
                    SerialNumber = i.SerialNumber,
                    ComponentId = i.ComponentId,
                    ComponentCode = i.Component.ComponentCode,
                    ComponentName = i.Component.ComponentName,
                    Status = i.Status,
                })
                .ToListAsync(ct);
        }
        else
        {
            audit.Differences = await InventoryTransactionService.ToResponses(
                    _uow.Repository<InventoryTransaction>().QueryNoTracking()
                        .Where(t => t.AuditId == auditId)
                        .OrderBy(t => t.Component.ComponentCode)
                        .ThenBy(t => t.Item != null ? t.Item.ItemCode : null)
                        .ThenBy(t => t.TransactionId))
                .ToListAsync(ct);
        }

        return audit;
    }

    public async Task<InventoryAuditDetailResponse> StartAuditAsync(
        StartInventoryAuditRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var cabinetStatus = await _uow.Repository<StorageCabinet>().QueryNoTracking()
                                .Where(c => c.CabinetId == request.CabinetId)
                                .Select(c => c.Status)
                                .FirstOrDefaultAsync(ct)
                            ?? throw new NotFoundException("Không tìm thấy tủ");
        if (cabinetStatus != CabinetStatuses.Active)
            throw new BadRequestException("Tủ đã ngừng hoạt động");

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await _uow.Repository<StorageCabinet>().Query()
            .Where(c => c.CabinetId == request.CabinetId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, c => c.Status), ct);

        var audits = _uow.Repository<InventoryAudit>();
        if (await audits.QueryNoTracking()
                .AnyAsync(a => a.CabinetId == request.CabinetId && a.Status == InventoryAuditStatuses.InProgress, ct))
            throw new ConflictException("Tủ này đang có phiên kiểm kê chưa hoàn tất");

        var audit = new InventoryAudit
        {
            CabinetId = request.CabinetId,
            AuditorId = userId,
            AuditDate = DateTime.UtcNow,
            Status = InventoryAuditStatuses.InProgress,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };
        await audits.AddAsync(audit, ct);
        await _uow.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return await GetAuditByIdAsync(audit.AuditId, ct);
    }

    public async Task<InventoryAuditDetailResponse> CompleteAuditAsync(
        Guid auditId, CompleteInventoryAuditRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var audit = await _uow.Repository<InventoryAudit>().QueryNoTracking()
                        .Where(a => a.AuditId == auditId)
                        .Select(a => new { a.CabinetId, a.Status, a.Note })
                        .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException("Không tìm thấy phiên kiểm kê");
        if (audit.Status != InventoryAuditStatuses.InProgress)
            throw new ConflictException("Phiên kiểm kê đã hoàn tất");

        var countedIds = request.Consumables.Select(l => l.ComponentId).ToList();
        if (countedIds.Count > 0)
        {
            var consumableIds = await _uow.Repository<Component>().QueryNoTracking()
                .Where(c => countedIds.Contains(c.ComponentId) && c.TrackingType == ComponentTrackingTypes.Consumable)
                .Select(c => c.ComponentId)
                .ToListAsync(ct);
            if (consumableIds.Count < countedIds.Count)
                throw new BadRequestException(
                    $"Có {countedIds.Count - consumableIds.Count} dòng vật tư không phải linh kiện tiêu hao, linh kiện theo từng con hãy báo trong danh sách con bị mất");
        }

        var now = DateTime.UtcNow;
        await using var tx = await _uow.BeginTransactionAsync(ct);

        var note = audit.Note;
        if (!string.IsNullOrWhiteSpace(request.Note))
            note = request.Note.Trim();

        var closed = await _uow.Repository<InventoryAudit>().Query()
            .Where(a => a.AuditId == auditId && a.Status == InventoryAuditStatuses.InProgress)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, InventoryAuditStatuses.Completed)
                .SetProperty(a => a.Note, note), ct);
        if (closed == 0)
            throw new ConflictException("Phiên kiểm kê vừa được hoàn tất hoặc hủy bởi người khác");

        var transactions = _uow.Repository<InventoryTransaction>();
        await AdjustConsumablesAsync(auditId, audit.CabinetId, request.Consumables, userId, now, ct);

        if (request.MissingItemIds.Count > 0)
        {
            var missingIds = request.MissingItemIds;
            var items = await _uow.Repository<ComponentItem>().QueryNoTracking()
                .Where(i => missingIds.Contains(i.ItemId)
                            && i.CabinetId == audit.CabinetId
                            && InCabinetStatuses.Contains(i.Status))
                .Select(i => new { i.ItemId, i.ComponentId, i.Status })
                .ToListAsync(ct);
            if (items.Count < missingIds.Count)
                throw new BadRequestException(
                    $"Có {missingIds.Count - items.Count} con không nằm trong tủ này hoặc không ở trạng thái {string.Join("/", InCabinetStatuses)}");

            var lost = await _uow.Repository<ComponentItem>().Query()
                .Where(i => missingIds.Contains(i.ItemId)
                            && i.CabinetId == audit.CabinetId
                            && InCabinetStatuses.Contains(i.Status))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.Status, ComponentItemStatuses.Lost)
                    .SetProperty(i => i.UpdatedAt, now), ct);
            if (lost != missingIds.Count)
                throw new ConflictException("Linh kiện vừa bị thay đổi bởi thao tác khác, vui lòng tải lại và kiểm tra lại");

            foreach (var item in items)
            {
                await transactions.AddAsync(new InventoryTransaction
                {
                    Type = InventoryTransactionTypes.Retire,
                    ComponentId = item.ComponentId,
                    CabinetId = audit.CabinetId,
                    ItemId = item.ItemId,
                    QuantityChange = -1,
                    Reason = $"{ComponentItemStatuses.Lost}: kiểm kê không tìm thấy (trước đó {item.Status})",
                    AuditId = auditId,
                    CreatedBy = userId,
                    CreatedAt = now,
                }, ct);
            }
        }

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Tồn kho vừa thay đổi bởi thao tác khác, vui lòng thử lại");
        }

        await tx.CommitAsync(ct);

        return await GetAuditByIdAsync(auditId, ct);
    }

    public async Task CancelAuditAsync(Guid auditId, CancellationToken ct = default)
    {
        var status = await _uow.Repository<InventoryAudit>().QueryNoTracking()
                         .Where(a => a.AuditId == auditId)
                         .Select(a => a.Status)
                         .FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Không tìm thấy phiên kiểm kê");
        if (status != InventoryAuditStatuses.InProgress)
            throw new ConflictException("Phiên kiểm kê đã hoàn tất, không hủy được");

        var deleted = await _uow.Repository<InventoryAudit>().Query()
            .Where(a => a.AuditId == auditId && a.Status == InventoryAuditStatuses.InProgress)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0)
            throw new ConflictException("Phiên kiểm kê vừa được hoàn tất bởi người khác");
    }

    /// <summary>
    /// Sets each counted CONSUMABLE to the counted quantity; only components whose quantity differs are written
    /// (one Adjust line each). Must run inside the caller's transaction.
    /// </summary>
    private async Task AdjustConsumablesAsync(
        Guid auditId, Guid cabinetId, IReadOnlyList<AuditConsumableCountRequest> counts, Guid userId, DateTime now, CancellationToken ct)
    {
        if (counts.Count == 0)
            return;

        var countedIds = counts.Select(l => l.ComponentId).ToList();
        var stocks = _uow.Repository<ConsumableStock>();
        var systemQuantities = await stocks.QueryNoTracking()
            .Where(st => st.CabinetId == cabinetId && countedIds.Contains(st.ComponentId))
            .ToDictionaryAsync(st => st.ComponentId, st => st.Quantity, ct);

        var transactions = _uow.Repository<InventoryTransaction>();
        foreach (var count in counts)
        {
            var hasRow = systemQuantities.TryGetValue(count.ComponentId, out var oldQuantity);
            if (oldQuantity == count.CountedQuantity)
                continue;

            if (hasRow)
            {
                var updated = await stocks.Query()
                    .Where(st => st.ComponentId == count.ComponentId
                                 && st.CabinetId == cabinetId
                                 && st.Quantity == oldQuantity)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(st => st.Quantity, count.CountedQuantity)
                        .SetProperty(st => st.UpdatedAt, now), ct);
                if (updated == 0)
                    throw new ConflictException("Tồn kho vừa thay đổi bởi thao tác khác, vui lòng tải lại và kiểm tra lại");
            }
            else
            {
                await stocks.AddAsync(new ConsumableStock
                {
                    ComponentId = count.ComponentId,
                    CabinetId = cabinetId,
                    Quantity = count.CountedQuantity,
                    UpdatedAt = now,
                }, ct);
            }

            await transactions.AddAsync(new InventoryTransaction
            {
                Type = InventoryTransactionTypes.Adjust,
                ComponentId = count.ComponentId,
                CabinetId = cabinetId,
                QuantityChange = count.CountedQuantity - oldQuantity,
                Reason = $"Kiểm kê: hệ thống {oldQuantity}, đếm được {count.CountedQuantity}",
                AuditId = auditId,
                CreatedBy = userId,
                CreatedAt = now,
            }, ct);
        }
    }
}

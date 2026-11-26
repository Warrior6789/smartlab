using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.BLL.Mappings.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

/// <summary>
/// Inventory count of one cabinet: only lab staff of the cabinet's lab count it (InProgress) and submit the result
/// (Submitted); an approver (INVENTORY_AUDIT_APPROVE, not the person who counted) accepts it (Completed: stock is
/// adjusted, missing units become Lost, every change goes to inventory_transactions with the audit id) or sends it
/// back for a recount (InProgress again). Stock is never touched before approval.
/// </summary>
public class InventoryAuditService : IInventoryAuditService
{
    private const string RecountHint = "Hãy yêu cầu đếm lại";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationSender _notifications;

    public InventoryAuditService(IUnitOfWork uow, ICurrentUserService currentUser, INotificationSender notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    private bool IsApprover => _currentUser.HasPermission(PermissionCodes.InventoryAuditApprove);

    public Task<PagedResult<InventoryAuditResponse>> GetAuditsAsync(
        InventoryAuditQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var audits = _uow.Repository<InventoryAudit>().QueryNoTracking();

        if (!IsApprover)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedException();
            audits = audits.Where(a => a.Cabinet.Room.LabRoomStaffs.Any(s => s.UserId == userId));
        }

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
                SubmittedAt = a.SubmittedAt,
                ReviewedById = a.ReviewedBy,
                ReviewedByName = a.ReviewedByNavigation != null ? a.ReviewedByNavigation.FullName : null,
                ReviewedAt = a.ReviewedAt,
                ReviewNote = a.ReviewNote,
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
                            SubmittedAt = a.SubmittedAt,
                            ReviewedById = a.ReviewedBy,
                            ReviewedByName = a.ReviewedByNavigation != null ? a.ReviewedByNavigation.FullName : null,
                            ReviewedAt = a.ReviewedAt,
                            ReviewNote = a.ReviewNote,
                            DifferenceCount = a.InventoryTransactions.Count,
                        })
                        .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException("Không tìm thấy phiên kiểm kê");

        await EnsureLabStaffOfCabinetAsync(audit.CabinetId, ct);

        if (audit.Status == InventoryAuditStatuses.InProgress)
        {
            audit.Consumables = await _uow.Repository<ConsumableStock>().QueryNoTracking()
                .Where(st => st.CabinetId == audit.CabinetId && st.Quantity > 0)
                .OrderBy(st => st.Component.ComponentCode)
                .Select(st => new AuditConsumableLineResponse
                {
                    ComponentId = st.ComponentId,
                    ComponentCode = st.Component.ShortCode ?? st.Component.ComponentCode,
                    ComponentName = st.Component.ComponentName,
                    SystemQuantity = st.Quantity,
                })
                .ToListAsync(ct);

            audit.Items = await _uow.Repository<ComponentItem>().QueryNoTracking()
                .Where(i => i.CabinetId == audit.CabinetId && i.Status == ComponentItemStatuses.Available)
                .OrderBy(i => i.Component.ComponentCode).ThenBy(i => i.ItemCode)
                .Select(i => new AuditItemLineResponse
                {
                    ItemId = i.ItemId,
                    ItemCode = i.ItemCode,
                    SerialNumber = i.SerialNumber,
                    ComponentId = i.ComponentId,
                    ComponentCode = i.Component.ShortCode ?? i.Component.ComponentCode,
                    ComponentName = i.Component.ComponentName,
                    Status = i.Status,
                })
                .ToListAsync(ct);

            var draft = await _uow.Repository<InventoryAudit>().QueryNoTracking()
                .Where(a => a.AuditId == auditId)
                .Select(a => new { a.Draft, a.DraftSavedAt })
                .FirstAsync(ct);
            audit.Draft = InventoryAuditDraftMappings.FromJson(draft.Draft, draft.DraftSavedAt);
        }
        else if (audit.Status == InventoryAuditStatuses.Submitted)
        {
            audit.SubmittedCounts = await _uow.Repository<InventoryAuditCount>().QueryNoTracking()
                .Where(c => c.AuditId == auditId)
                .OrderBy(c => c.Component.ComponentCode)
                .Select(c => new AuditSubmittedCountResponse
                {
                    ComponentId = c.ComponentId,
                    ComponentCode = c.Component.ShortCode ?? c.Component.ComponentCode,
                    ComponentName = c.Component.ComponentName,
                    SystemQuantity = c.SystemQuantity,
                    CountedQuantity = c.CountedQuantity,
                    Difference = c.CountedQuantity - c.SystemQuantity,
                })
                .ToListAsync(ct);

            audit.MissingItems = await _uow.Repository<InventoryAudit>().QueryNoTracking()
                .Where(a => a.AuditId == auditId)
                .SelectMany(a => a.Items)
                .OrderBy(i => i.Component.ComponentCode).ThenBy(i => i.ItemCode)
                .Select(i => new AuditItemLineResponse
                {
                    ItemId = i.ItemId,
                    ItemCode = i.ItemCode,
                    SerialNumber = i.SerialNumber,
                    ComponentId = i.ComponentId,
                    ComponentCode = i.Component.ShortCode ?? i.Component.ComponentCode,
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

        await EnsureCountedByLabStaffAsync(request.CabinetId, ct);

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await _uow.Repository<StorageCabinet>().Query()
            .Where(c => c.CabinetId == request.CabinetId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, c => c.Status), ct);

        var audits = _uow.Repository<InventoryAudit>();
        var open = await audits.QueryNoTracking()
            .AnyAsync(a => a.CabinetId == request.CabinetId
                           && (a.Status == InventoryAuditStatuses.InProgress || a.Status == InventoryAuditStatuses.Submitted), ct);
        if (open)
            throw new ConflictException("Tủ này đang có phiên kiểm kê chưa xong (đang kiểm hoặc chờ duyệt)");

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

    public async Task<AuditDraftResponse> SaveDraftAsync(
        Guid auditId, SaveInventoryAuditDraftRequest request, CancellationToken ct = default)
    {
        var audit = await _uow.Repository<InventoryAudit>().QueryNoTracking()
                        .Where(a => a.AuditId == auditId)
                        .Select(a => new { a.CabinetId, a.Status })
                        .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException("Không tìm thấy phiên kiểm kê");
        await EnsureCountedByLabStaffAsync(audit.CabinetId, ct);
        if (audit.Status != InventoryAuditStatuses.InProgress)
            throw new ConflictException("Phiên kiểm kê không ở trạng thái đang kiểm");

        var json = InventoryAuditDraftMappings.ToJson(request);
        string? note = null;
        if (!string.IsNullOrWhiteSpace(request.Note))
            note = request.Note.Trim();
        var now = DateTime.UtcNow;

        var saved = await _uow.Repository<InventoryAudit>().Query()
            .Where(a => a.AuditId == auditId && a.Status == InventoryAuditStatuses.InProgress)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Draft, json)
                .SetProperty(a => a.DraftSavedAt, now)
                .SetProperty(a => a.Note, note), ct);
        if (saved == 0)
            throw new ConflictException("Phiên kiểm kê vừa được gửi hoặc hủy bởi người khác");

        return InventoryAuditDraftMappings.FromJson(json, now)!;
    }

    public async Task<InventoryAuditDetailResponse> SubmitAuditAsync(
        Guid auditId, SubmitInventoryAuditRequest request, CancellationToken ct = default)
    {
        var audit = await _uow.Repository<InventoryAudit>().QueryNoTracking()
                        .Where(a => a.AuditId == auditId)
                        .Select(a => new { a.CabinetId, a.Status, a.Note })
                        .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException("Không tìm thấy phiên kiểm kê");
        await EnsureCountedByLabStaffAsync(audit.CabinetId, ct);
        if (audit.Status != InventoryAuditStatuses.InProgress)
            throw new ConflictException("Phiên kiểm kê không ở trạng thái đang kiểm");

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

        var missingIds = request.MissingItemIds;
        var now = DateTime.UtcNow;
        await using var tx = await _uow.BeginTransactionAsync(ct);

        var note = audit.Note;
        if (!string.IsNullOrWhiteSpace(request.Note))
            note = request.Note.Trim();

        var submitted = await _uow.Repository<InventoryAudit>().Query()
            .Where(a => a.AuditId == auditId && a.Status == InventoryAuditStatuses.InProgress)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, InventoryAuditStatuses.Submitted)
                .SetProperty(a => a.SubmittedAt, now)
                .SetProperty(a => a.Note, note)
                .SetProperty(a => a.Draft, (string?)null)
                .SetProperty(a => a.DraftSavedAt, (DateTime?)null), ct);
        if (submitted == 0)
            throw new ConflictException("Phiên kiểm kê vừa được gửi hoặc hủy bởi người khác");

        var tracked = await _uow.Repository<InventoryAudit>().Query()
            .Include(a => a.Items)
            .FirstAsync(a => a.AuditId == auditId, ct);
        tracked.Items.Clear();
        await _uow.Repository<InventoryAuditCount>().Query()
            .Where(c => c.AuditId == auditId)
            .ExecuteDeleteAsync(ct);

        if (missingIds.Count > 0)
        {
            var items = await _uow.Repository<ComponentItem>().Query()
                .Where(i => missingIds.Contains(i.ItemId)
                            && i.CabinetId == audit.CabinetId
                            && i.Status == ComponentItemStatuses.Available)
                .ToListAsync(ct);
            if (items.Count < missingIds.Count)
                throw new BadRequestException(
                    $"Có {missingIds.Count - items.Count} con không nằm trong tủ này hoặc không ở trạng thái {ComponentItemStatuses.Available}");

            foreach (var item in items)
                tracked.Items.Add(item);
        }

        if (countedIds.Count > 0)
        {
            var systemQuantities = await _uow.Repository<ConsumableStock>().QueryNoTracking()
                .Where(st => st.CabinetId == audit.CabinetId && countedIds.Contains(st.ComponentId))
                .ToDictionaryAsync(st => st.ComponentId, st => st.Quantity, ct);

            var foundMoreIds = request.Consumables
                .Where(c => c.CountedQuantity > systemQuantities.GetValueOrDefault(c.ComponentId))
                .Select(c => c.ComponentId)
                .ToList();
            await CabinetRules.EnsureAcceptsAsync(_uow, audit.CabinetId, foundMoreIds, ct);

            foreach (var count in request.Consumables)
            {
                tracked.InventoryAuditCounts.Add(new InventoryAuditCount
                {
                    ComponentId = count.ComponentId,
                    SystemQuantity = systemQuantities.GetValueOrDefault(count.ComponentId),
                    CountedQuantity = count.CountedQuantity
                                      ?? throw new BadRequestException("Số đếm thực tế là bắt buộc"),
                });
            }
        }

        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await NotifyManagersSubmittedAsync(auditId, audit.CabinetId, ct);

        return await GetAuditByIdAsync(auditId, ct);
    }

    public async Task<InventoryAuditDetailResponse> ApproveAuditAsync(
        Guid auditId, ApproveInventoryAuditRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var audit = await _uow.Repository<InventoryAudit>().QueryNoTracking()
                        .Where(a => a.AuditId == auditId)
                        .Select(a => new { a.CabinetId, a.Status, a.AuditorId })
                        .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException("Không tìm thấy phiên kiểm kê");
        if (audit.Status != InventoryAuditStatuses.Submitted)
            throw new ConflictException("Chỉ duyệt được phiên kiểm kê đang chờ duyệt");
        if (audit.AuditorId == userId && !_currentUser.IsInRole(RoleNames.Admin))
            throw new ForbiddenException("Người duyệt phải khác người đã kiểm kê tủ này");

        var now = DateTime.UtcNow;
        await using var tx = await _uow.BeginTransactionAsync(ct);

        var approved = await _uow.Repository<InventoryAudit>().Query()
            .Where(a => a.AuditId == auditId && a.Status == InventoryAuditStatuses.Submitted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, InventoryAuditStatuses.Completed)
                .SetProperty(a => a.ReviewedBy, (Guid?)userId)
                .SetProperty(a => a.ReviewedAt, (DateTime?)now)
                .SetProperty(a => a.ReviewNote, string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()), ct);
        if (approved == 0)
            throw new ConflictException("Phiên kiểm kê vừa được duyệt hoặc trả lại bởi người khác");

        await ApplyConsumableCountsAsync(auditId, audit.CabinetId, userId, now, ct);
        await MarkMissingItemsLostAsync(auditId, audit.CabinetId, userId, now, ct);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Tồn kho vừa thay đổi bởi thao tác khác, vui lòng thử lại");
        }

        await tx.CommitAsync(ct);

        await NotifyAuditorAsync(auditId, audit.AuditorId, audit.CabinetId, "đã được duyệt", ct);

        var detail = await GetAuditByIdAsync(auditId, ct);

        var touchedIds = detail.Differences.Select(d => d.ComponentId).Distinct().ToList();
        var roomId = await RoomStock.GetRoomIdAsync(_uow, audit.CabinetId, ct);
        detail.ReservationShortages = await RoomStock.GetShortagesAsync(_uow, roomId, touchedIds, ct);

        return detail;
    }

    public async Task<InventoryAuditDetailResponse> RequestRecountAsync(
        Guid auditId, RecountInventoryAuditRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var audit = await _uow.Repository<InventoryAudit>().QueryNoTracking()
                        .Where(a => a.AuditId == auditId)
                        .Select(a => new { a.CabinetId, a.Status, a.AuditorId })
                        .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException("Không tìm thấy phiên kiểm kê");
        if (audit.Status != InventoryAuditStatuses.Submitted)
            throw new ConflictException("Chỉ yêu cầu đếm lại được phiên kiểm kê đang chờ duyệt");

        var now = DateTime.UtcNow;
        await using var tx = await _uow.BeginTransactionAsync(ct);

        var reopened = await _uow.Repository<InventoryAudit>().Query()
            .Where(a => a.AuditId == auditId && a.Status == InventoryAuditStatuses.Submitted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, InventoryAuditStatuses.InProgress)
                .SetProperty(a => a.ReviewedBy, (Guid?)userId)
                .SetProperty(a => a.ReviewedAt, (DateTime?)now)
                .SetProperty(a => a.ReviewNote, request.Reason.Trim()), ct);
        if (reopened == 0)
            throw new ConflictException("Phiên kiểm kê vừa được duyệt hoặc trả lại bởi người khác");

        var tracked = await _uow.Repository<InventoryAudit>().Query()
            .Include(a => a.Items)
            .FirstAsync(a => a.AuditId == auditId, ct);
        tracked.Items.Clear();
        await _uow.Repository<InventoryAuditCount>().Query()
            .Where(c => c.AuditId == auditId)
            .ExecuteDeleteAsync(ct);

        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await NotifyAuditorAsync(auditId, audit.AuditorId, audit.CabinetId, "bị yêu cầu đếm lại", ct);

        return await GetAuditByIdAsync(auditId, ct);
    }

    public async Task CancelAuditAsync(Guid auditId, CancellationToken ct = default)
    {
        var audit = await _uow.Repository<InventoryAudit>().QueryNoTracking()
                        .Where(a => a.AuditId == auditId)
                        .Select(a => new { a.CabinetId, a.Status })
                        .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException("Không tìm thấy phiên kiểm kê");
        await EnsureCountedByLabStaffAsync(audit.CabinetId, ct);
        if (audit.Status != InventoryAuditStatuses.InProgress)
            throw new ConflictException("Chỉ hủy được phiên đang kiểm (phiên chờ duyệt phải được duyệt hoặc yêu cầu đếm lại)");

        var deleted = await _uow.Repository<InventoryAudit>().Query()
            .Where(a => a.AuditId == auditId && a.Status == InventoryAuditStatuses.InProgress)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0)
            throw new ConflictException("Phiên kiểm kê vừa được gửi hoặc hủy bởi người khác");
    }

    /// <summary>
    /// Approvers may work on every cabinet; other users (Lab Staff) only on cabinets of a lab they are assigned to.
    /// </summary>
    /// <summary>Viewing an audit: approvers see every cabinet, lab staff only the cabinets of their labs.</summary>
    private async Task NotifyManagersSubmittedAsync(Guid auditId, Guid cabinetId, CancellationToken ct)
    {
        var cabinetName = await CabinetNameAsync(cabinetId, ct);
        var managerIds = await _uow.Repository<User>().QueryNoTracking()
            .Where(u => u.IsActive && u.UserRoles.Any(r => r.Role.RoleName == RoleNames.InventoryManager))
            .Select(u => u.UserId)
            .ToListAsync(ct);

        foreach (var managerId in managerIds)
            await _notifications.SendAsync(managerId, $"Kiểm kê {cabinetName} đang chờ duyệt",
                NotificationRefTypes.InventoryAudit, auditId);
    }

    private async Task NotifyAuditorAsync(Guid auditId, Guid auditorId, Guid cabinetId, string outcome, CancellationToken ct)
    {
        var cabinetName = await CabinetNameAsync(cabinetId, ct);
        await _notifications.SendAsync(auditorId, $"Kiểm kê {cabinetName} {outcome}",
            NotificationRefTypes.InventoryAudit, auditId);
    }

    private Task<string> CabinetNameAsync(Guid cabinetId, CancellationToken ct) =>
        _uow.Repository<StorageCabinet>().QueryNoTracking()
            .Where(c => c.CabinetId == cabinetId)
            .Select(c => c.CabinetName + " (" + c.Room.RoomCode + ")")
            .FirstAsync(ct);

    private async Task EnsureLabStaffOfCabinetAsync(Guid cabinetId, CancellationToken ct)
    {
        if (IsApprover)
            return;

        await EnsureCountedByLabStaffAsync(cabinetId, ct);
    }

    /// <summary>
    /// Counting (start, save, submit, cancel) is done only by the staff assigned to the cabinet's lab; approvers only
    /// view and approve, so the person who counts and the person who approves stay apart.
    /// </summary>
    private async Task EnsureCountedByLabStaffAsync(Guid cabinetId, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var assigned = await _uow.Repository<StorageCabinet>().QueryNoTracking()
            .AnyAsync(c => c.CabinetId == cabinetId && c.Room.LabRoomStaffs.Any(s => s.UserId == userId), ct);
        if (!assigned)
            throw new ForbiddenException("Chỉ nhân viên phụ trách lab của tủ này được kiểm kê tủ");
    }

    /// <summary>
    /// Adds counted - system (both as at submit) to today's stock, so loans and returns made while the count waited for
    /// approval stay correct. One Adjust line per component that differs. Must run inside the caller's transaction.
    /// </summary>
    private async Task ApplyConsumableCountsAsync(Guid auditId, Guid cabinetId, Guid userId, DateTime now, CancellationToken ct)
    {
        var counts = await _uow.Repository<InventoryAuditCount>().QueryNoTracking()
            .Where(c => c.AuditId == auditId && c.CountedQuantity != c.SystemQuantity)
            .ToListAsync(ct);

        var stocks = _uow.Repository<ConsumableStock>();
        var transactions = _uow.Repository<InventoryTransaction>();
        foreach (var count in counts)
        {
            var change = count.CountedQuantity - count.SystemQuantity;
            var updated = await stocks.Query()
                .Where(st => st.ComponentId == count.ComponentId
                             && st.CabinetId == cabinetId
                             && st.Quantity + change >= 0)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(st => st.Quantity, st => st.Quantity + change)
                    .SetProperty(st => st.UpdatedAt, now), ct);

            if (updated == 0)
            {
                var hasRow = await stocks.QueryNoTracking()
                    .AnyAsync(st => st.ComponentId == count.ComponentId && st.CabinetId == cabinetId, ct);
                if (hasRow || change < 0)
                    throw new ConflictException(
                        $"Tồn hiện tại không đủ để trừ phần thiếu của kiểm kê (đã có xuất kho sau khi đếm). {RecountHint}");

                await stocks.AddAsync(new ConsumableStock
                {
                    ComponentId = count.ComponentId,
                    CabinetId = cabinetId,
                    Quantity = change,
                    UpdatedAt = now,
                }, ct);
            }

            await transactions.AddAsync(new InventoryTransaction
            {
                Type = InventoryTransactionTypes.Adjust,
                ComponentId = count.ComponentId,
                CabinetId = cabinetId,
                QuantityChange = change,
                Reason = $"Kiểm kê: hệ thống {count.SystemQuantity}, đếm được {count.CountedQuantity}",
                AuditId = auditId,
                CreatedBy = userId,
                CreatedAt = now,
            }, ct);
        }
    }

    /// <summary>
    /// Units reported missing become Lost (one Retire line each). They must still be Available in the cabinet: one that
    /// was handed out or moved after the count means it was not really missing, so the count must be redone.
    /// </summary>
    private async Task MarkMissingItemsLostAsync(Guid auditId, Guid cabinetId, Guid userId, DateTime now, CancellationToken ct)
    {
        var missing = await _uow.Repository<InventoryAudit>().QueryNoTracking()
            .Where(a => a.AuditId == auditId)
            .SelectMany(a => a.Items)
            .Select(i => new { i.ItemId, i.ComponentId })
            .ToListAsync(ct);
        if (missing.Count == 0)
            return;

        var missingIds = missing.Select(m => m.ItemId).ToList();
        var lost = await _uow.Repository<ComponentItem>().Query()
            .Where(i => missingIds.Contains(i.ItemId)
                        && i.CabinetId == cabinetId
                        && i.Status == ComponentItemStatuses.Available)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, ComponentItemStatuses.Lost)
                .SetProperty(i => i.UpdatedAt, now), ct);
        if (lost != missingIds.Count)
            throw new ConflictException(
                $"Có con báo thiếu đã được giao, chuyển tủ hoặc đổi trạng thái sau khi đếm. {RecountHint}");

        var transactions = _uow.Repository<InventoryTransaction>();
        foreach (var item in missing)
        {
            await transactions.AddAsync(new InventoryTransaction
            {
                Type = InventoryTransactionTypes.Retire,
                ComponentId = item.ComponentId,
                CabinetId = cabinetId,
                ItemId = item.ItemId,
                QuantityChange = -1,
                Reason = $"{ComponentItemStatuses.Lost}: kiểm kê không tìm thấy",
                AuditId = auditId,
                CreatedBy = userId,
                CreatedAt = now,
            }, ct);
        }
    }
}

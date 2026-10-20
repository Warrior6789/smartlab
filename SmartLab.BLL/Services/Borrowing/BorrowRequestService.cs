using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;
using SmartLab.BLL.DTOs.IssueReports;
using SmartLab.BLL.External.Ai;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.External.Storage;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.BLL.Interfaces.IssueReports;
using SmartLab.BLL.Mappings.Borrowing;
using SmartLab.BLL.Services.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Borrowing after the lecturer approved (see ProjectReviewService): lines from other labs are carried to the common
/// pickup lab and received there (Approved → Ready), the student picks everything up with a photo of the components and
/// the student card (Ready → Borrowing), and returns at the pickup lab, possibly in several times (→ Returned).
/// A unit that breaks during the loan can be swapped: the broken one is retired and a Pending replacement line is added
/// to the same request, taken from the pickup lab or carried over from another lab, then handed over on its own.
/// Every change first locks the request row (a no-op UPDATE ... WHERE status = ...), so two staff cannot act on the same
/// request at once. Lock order is always request → labs.
/// </summary>
public class BorrowRequestService : IBorrowRequestService
{
    private const string StorageFolder = "borrow-handover";
    private const string StockChangedMessage = "Tồn kho vừa thay đổi bởi thao tác khác, vui lòng thử lại";

    private static readonly string[] BorrowedStatuses = { BorrowRequestStatuses.Borrowing, BorrowRequestStatuses.Overdue };

    /// <summary>Request statuses with lines to carry over: Approved, or a replacement of a request being borrowed.</summary>
    private static readonly string[] TransferStatuses =
        { BorrowRequestStatuses.Approved, BorrowRequestStatuses.Borrowing, BorrowRequestStatuses.Overdue };

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorage _fileStorage;
    private readonly IStudentCardChecker _cardChecker;
    private readonly IBorrowNotifier _notifier;
    private readonly IIssueReportBorrowingIntegration _issueReports;
    private readonly ILogger<BorrowRequestService> _logger;

    public BorrowRequestService(
        IUnitOfWork uow, ICurrentUserService currentUser, IFileStorage fileStorage,
        IStudentCardChecker cardChecker, IBorrowNotifier notifier,
        IIssueReportBorrowingIntegration issueReports, ILogger<BorrowRequestService> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _cardChecker = cardChecker;
        _notifier = notifier;
        _issueReports = issueReports;
        _logger = logger;
    }

    public Task<PagedResult<BorrowRequestResponse>> GetMyRequestsAsync(
        BorrowRequestQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var requests = _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.RequesterId == userId
                        || (r.Project != null && r.Project.Team != null
                            && (r.Project.Team.Leader.UserId == userId
                                || r.Project.Team.TeamMembers.Any(tm => tm.Student.UserId == userId
                                                                        && tm.Status == TeamMemberStatuses.Accepted))));

        return ToPagedResponses(Filter(requests, query), paging, ct);
    }

    public Task<PagedResult<BorrowRequestResponse>> GetRequestsAsync(
        BorrowRequestQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var requests = _uow.Repository<BorrowRequest>().QueryNoTracking();

        if (!_currentUser.IsInRole(RoleNames.Admin))
        {
            var myRooms = MyRoomIds();
            requests = requests.Where(r => myRooms.Contains(r.RoomId)
                                           || r.BorrowItems.Any(i => i.SourceRoomId != null && myRooms.Contains(i.SourceRoomId.Value)));
        }

        return ToPagedResponses(Filter(requests, query), paging, ct);
    }

    public async Task<BorrowRequestDetailResponse> GetRequestByIdAsync(Guid borrowRequestId, CancellationToken ct = default)
    {
        if (!await _uow.Repository<BorrowRequest>().QueryNoTracking().AnyAsync(r => r.BorrowRequestId == borrowRequestId, ct))
            throw new NotFoundException("Không tìm thấy đơn mượn");

        var overdueBefore = OverdueBefore();
        return await VisibleRequests()
                   .Where(r => r.BorrowRequestId == borrowRequestId)
                   .Select(r => new BorrowRequestDetailResponse
                   {
                       BorrowRequestId = r.BorrowRequestId,
                       RequestCode = r.RequestCode,
                       Status = r.Status,
                       IsOverdue = BorrowedStatuses.Contains(r.Status) && r.DueDate < overdueBefore,
                       RequesterId = r.RequesterId,
                       RequesterName = r.Requester.FullName,
                       StudentCode = r.Requester.StudentProfile != null ? r.Requester.StudentProfile.StudentCode : null,
                       ClassId = r.ClassId,
                       ClassCode = r.Class != null ? r.Class.ClassCode : null,
                       ProjectId = r.ProjectId,
                       ProjectTitle = r.Project != null ? r.Project.Title : null,
                       RoomId = r.RoomId,
                       RoomCode = r.Room.RoomCode,
                       RoomName = r.Room.RoomName,
                       RequestDate = r.RequestDate,
                       ApprovedAt = r.ApprovedAt,
                       BorrowDate = r.BorrowDate,
                       DueDate = r.DueDate,
                       ReturnedAt = r.ReturnedAt,
                       ItemCount = r.BorrowItems.Count,
                       WaitingTransferCount = r.BorrowItems.Count(i => i.SourceRoomId != null && i.ReceivedAt == null),
                       Purpose = r.Purpose,
                       RejectReason = r.RejectReason,
                       Items = r.BorrowItems
                           .OrderBy(i => i.Component.ComponentCode).ThenBy(i => i.BorrowItemId)
                           .Select(i => new BorrowItemResponse
                           {
                               BorrowItemId = i.BorrowItemId,
                               ComponentId = i.ComponentId,
                               ComponentCode = i.Component.ComponentCode,
                               ComponentName = i.Component.ComponentName,
                               TrackingType = i.Component.TrackingType,
                               Unit = i.Component.Unit,
                               Quantity = i.Quantity,
                               Status = i.Status,
                               SourceRoomId = i.SourceRoomId,
                               SourceRoomCode = i.SourceRoom != null ? i.SourceRoom.RoomCode : null,
                               ReceivedAt = i.ReceivedAt,
                               ComponentItemId = i.ComponentItemId,
                               ItemCode = i.ComponentItem != null ? i.ComponentItem.ItemCode : null,
                               SerialNumber = i.ComponentItem != null ? i.ComponentItem.SerialNumber : null,
                               IssuedAt = i.IssuedAt,
                               ReturnedAt = i.ReturnedAt,
                               ReturnedQuantity = i.ReturnedQuantity,
                               ReturnCondition = i.ReturnCondition,
                               ReturnPhotoId = i.ReturnPhotoId,
                           })
                           .ToList(),
                       Photos = r.BorrowHandoverPhotos
                           .OrderBy(p => p.TakenAt).ThenBy(p => p.PhotoId)
                           .Select(p => new HandoverPhotoResponse
                           {
                               PhotoId = p.PhotoId,
                               BorrowRequestId = p.BorrowRequestId,
                               PhotoType = p.PhotoType,
                               PhotoUrl = p.PhotoUrl,
                               AiResult = p.AiResult,
                               AiDetail = p.AiDetail,
                               OverrideReason = p.OverrideReason,
                               TakenBy = p.TakenBy,
                               TakenByName = p.TakenByNavigation.FullName,
                               TakenAt = p.TakenAt,
                           })
                           .ToList(),
                       Approvals = r.BorrowApprovals
                           .OrderBy(a => a.StepNo)
                           .Select(a => new BorrowApprovalResponse
                           {
                               StepNo = a.StepNo,
                               ApproverRole = a.ApproverRole,
                               ApproverId = a.ApproverId,
                               ApproverName = a.Approver.FullName,
                               Status = a.Status,
                               Comment = a.Comment,
                               DecidedAt = a.DecidedAt,
                           })
                           .ToList(),
                       Extensions = r.BorrowExtensions
                           .OrderBy(e => e.CreatedAt)
                           .Select(e => new BorrowExtensionResponse
                           {
                               ExtensionId = e.ExtensionId,
                               BorrowRequestId = e.BorrowRequestId,
                               RequestCode = r.RequestCode,
                               RequesterName = r.Requester.FullName,
                               ClassCode = r.Class != null ? r.Class.ClassCode : null,
                               ProjectTitle = r.Project != null ? r.Project.Title : null,
                               OldDueDate = e.OldDueDate,
                               NewDueDate = e.NewDueDate,
                               Reason = e.Reason,
                               Status = e.Status,
                               ReviewedBy = e.ReviewedBy,
                               ReviewedByName = e.ReviewedByNavigation != null ? e.ReviewedByNavigation.FullName : null,
                               ReviewComment = e.ReviewComment,
                               CreatedAt = e.CreatedAt,
                               ReviewedAt = e.ReviewedAt,
                           })
                           .ToList(),
                   })
                   .AsSplitQuery()
                   .FirstOrDefaultAsync(ct)
               ?? throw new ForbiddenException("Bạn không có quyền xem đơn mượn này");
    }

    public Task<PagedResult<TransferLineResponse>> GetTransfersAsync(
        TransferQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var lines = _uow.Repository<BorrowItem>().QueryNoTracking()
            .Where(i => i.SourceRoomId != null
                        && i.ReceivedAt == null
                        && i.Status == BorrowItemStatuses.Pending
                        && TransferStatuses.Contains(i.BorrowRequest.Status));

        if (!_currentUser.IsInRole(RoleNames.Admin))
        {
            var myRooms = MyRoomIds();
            lines = lines.Where(i => myRooms.Contains(i.SourceRoomId!.Value) || myRooms.Contains(i.BorrowRequest.RoomId));
        }

        if (query.SourceRoomId.HasValue)
            lines = lines.Where(i => i.SourceRoomId == query.SourceRoomId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            lines = lines.Where(i => i.BorrowRequest.RequestCode.ToLower().Contains(keyword)
                                     || i.Component.ComponentCode.ToLower().Contains(keyword)
                                     || i.Component.ComponentName.ToLower().Contains(keyword));
        }

        var result = lines
            .OrderBy(i => i.BorrowRequest.ApprovedAt).ThenBy(i => i.BorrowRequest.RequestCode).ThenBy(i => i.BorrowItemId)
            .Select(i => new TransferLineResponse
            {
                BorrowItemId = i.BorrowItemId,
                BorrowRequestId = i.BorrowRequestId,
                RequestCode = i.BorrowRequest.RequestCode,
                ComponentId = i.ComponentId,
                ComponentCode = i.Component.ComponentCode,
                ComponentName = i.Component.ComponentName,
                TrackingType = i.Component.TrackingType,
                Unit = i.Component.Unit,
                Quantity = i.Quantity,
                SourceRoomId = i.SourceRoomId!.Value,
                SourceRoomCode = i.SourceRoom!.RoomCode,
                PickupRoomId = i.BorrowRequest.RoomId,
                PickupRoomCode = i.BorrowRequest.Room.RoomCode,
                ApprovedAt = i.BorrowRequest.ApprovedAt,
                BorrowDate = i.BorrowRequest.BorrowDate,
            });

        return PagedResult<TransferLineResponse>.CreateAsync(result, paging, ct);
    }

    /// <summary>
    /// The pickup lab confirms one line arrived from its source lab: the units (or CONSUMABLE quantity, taken from the
    /// source lab's fullest cabinets) move into a cabinet of the pickup lab, logged as Move. When the last line of an
    /// Approved request arrives the request becomes Ready. A replacement line of a request being borrowed can then be
    /// handed over with <see cref="IssueReplacementAsync"/>.
    /// </summary>
    public async Task<BorrowRequestDetailResponse> ReceiveTransferAsync(
        Guid borrowRequestId, ReceiveTransferRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(borrowRequestId, TransferStatuses,
            "Chỉ nhận hàng chuyển về cho đơn đã duyệt đang chờ chuyển hàng hoặc món thay thế của đơn đang mượn", ct);
        var header = await GetHeaderAsync(borrowRequestId, ct);
        await EnsurePickupStaffAsync(header.RoomId, ct);

        var line = await _uow.Repository<BorrowItem>().Query()
                       .Include(i => i.Component)
                       .FirstOrDefaultAsync(i => i.BorrowItemId == request.BorrowItemId && i.BorrowRequestId == borrowRequestId, ct)
                   ?? throw new NotFoundException("Không tìm thấy dòng linh kiện trong đơn mượn");
        if (line.Status != BorrowItemStatuses.Pending || line.ReceivedAt != null || line.SourceRoomId == null)
            throw new ConflictException("Dòng linh kiện này đã được nhận ở lab nhận đồ");
        var sourceRoomId = line.SourceRoomId.Value;

        var cabinetOk = await _uow.Repository<StorageCabinet>().QueryNoTracking()
            .AnyAsync(c => c.CabinetId == request.ToCabinetId
                           && c.RoomId == header.RoomId
                           && c.Status == CabinetStatuses.Active, ct);
        if (!cabinetOk)
            throw new BadRequestException("Tủ cất phải là tủ đang hoạt động của lab nhận đồ");

        await RoomStock.LockRoomsAsync(_uow, new[] { sourceRoomId, header.RoomId }, ct);

        var transactions = _uow.Repository<InventoryTransaction>();
        if (line.Component.TrackingType == ComponentTrackingTypes.Consumable)
        {
            if (request.ItemIds.Count > 0)
                throw new BadRequestException("Vật tư tiêu hao không quét mã từng con, chỉ nhận theo số lượng");

            var taken = await TakeConsumableAsync(line.ComponentId, sourceRoomId, line.Quantity, now, ct);
            foreach (var (cabinetId, quantity) in taken)
                await transactions.AddAsync(NewTransaction(InventoryTransactionTypes.Move, line.ComponentId, cabinetId, null,
                    -quantity, borrowRequestId, header.RequestCode, userId, now), ct);

            await AddConsumableAsync(line.ComponentId, request.ToCabinetId, line.Quantity, now, ct);
            await transactions.AddAsync(NewTransaction(InventoryTransactionTypes.Move, line.ComponentId, request.ToCabinetId, null,
                line.Quantity, borrowRequestId, header.RequestCode, userId, now), ct);
        }
        else
        {
            if (request.ItemIds.Count != line.Quantity)
                throw new BadRequestException(
                    $"{line.Component.ComponentName}: phải quét đúng {line.Quantity} con, đã quét {request.ItemIds.Count}");

            var units = await LoadUnitsAsync(request.ItemIds, ct);
            var errors = CheckUnits(units, request.ItemIds, line.ComponentId, ComponentItemStatuses.Available, sourceRoomId,
                "không nằm ở lab chuyển hàng");
            if (errors.Count > 0)
                throw new BadRequestException("Có con không nhận được", new Dictionary<string, string[]> { ["itemIds"] = errors.ToArray() });

            var moved = await _uow.Repository<ComponentItem>().Query()
                .Where(i => request.ItemIds.Contains(i.ItemId)
                            && i.Status == ComponentItemStatuses.Available
                            && i.Cabinet.RoomId == sourceRoomId
                            && !i.IssueReports.Any(r => r.Status == IssueReportStatuses.Open))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.CabinetId, request.ToCabinetId)
                    .SetProperty(i => i.UpdatedAt, now), ct);
            if (moved != request.ItemIds.Count)
                throw new ConflictException(StockChangedMessage);

            foreach (var unit in units)
            {
                await transactions.AddAsync(NewTransaction(InventoryTransactionTypes.Move, line.ComponentId, unit.CabinetId, unit.ItemId,
                    -1, borrowRequestId, header.RequestCode, userId, now), ct);
                await transactions.AddAsync(NewTransaction(InventoryTransactionTypes.Move, line.ComponentId, request.ToCabinetId, unit.ItemId,
                    1, borrowRequestId, header.RequestCode, userId, now), ct);
            }
        }

        line.ReceivedAt = now;
        line.ReceivedBy = userId;
        await SaveStockChangesAsync(ct);

        if (header.Status != BorrowRequestStatuses.Approved)
        {
            await tx.CommitAsync(ct);
            await _notifier.ReplacementArrivedAsync(borrowRequestId, line.BorrowItemId, ct);
            return await GetRequestByIdAsync(borrowRequestId, ct);
        }

        var waiting = await _uow.Repository<BorrowItem>().QueryNoTracking()
            .AnyAsync(i => i.BorrowRequestId == borrowRequestId && i.ReceivedAt == null, ct);
        if (!waiting)
        {
            await _uow.Repository<BorrowRequest>().Query()
                .Where(r => r.BorrowRequestId == borrowRequestId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, BorrowRequestStatuses.Ready), ct);
        }

        await tx.CommitAsync(ct);

        if (!waiting)
            await _notifier.ReadyAsync(borrowRequestId, ct);

        return await GetRequestByIdAsync(borrowRequestId, ct);
    }

    /// <summary>
    /// Stores a handover photo. An Issue photo (request Ready, or a request being borrowed with a replacement at the pickup
    /// lab) is also read by the AI: the student code on the card is compared with the borrower's. The AI never blocks:
    /// when it fails the result is Unreadable.
    /// </summary>
    public async Task<HandoverPhotoResponse> UploadPhotoAsync(
        Guid borrowRequestId, UploadHandoverPhotoRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var info = await _uow.Repository<BorrowRequest>().QueryNoTracking()
                       .Where(r => r.BorrowRequestId == borrowRequestId)
                       .Select(r => new
                       {
                           r.Status,
                           r.RoomId,
                           StudentCode = r.Requester.StudentProfile != null ? r.Requester.StudentProfile.StudentCode : null,
                           ReplacementReady = r.BorrowItems.Any(i => i.Status == BorrowItemStatuses.Pending && i.ReceivedAt != null),
                       })
                       .FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Không tìm thấy đơn mượn");
        await EnsurePickupStaffAsync(info.RoomId, ct);

        var photoType = request.PhotoType;
        var replacementToIssue = BorrowedStatuses.Contains(info.Status) && info.ReplacementReady;
        if (photoType == HandoverPhotoTypes.Issue && info.Status != BorrowRequestStatuses.Ready && !replacementToIssue)
            throw new ConflictException(
                "Chỉ chụp ảnh giao đồ khi đơn đã đủ hàng ở lab nhận đồ (Ready) hoặc có món thay thế đã về lab nhận đồ");
        if (photoType == HandoverPhotoTypes.Return && !BorrowedStatuses.Contains(info.Status))
            throw new ConflictException("Chỉ chụp ảnh nhận trả khi đơn đang mượn");

        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            await request.File.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }

        string? aiResult = null;
        string? aiDetail = null;
        if (photoType == HandoverPhotoTypes.Issue)
        {
            var reading = await _cardChecker.ReadAsync(bytes, request.File.ContentType, ct);
            aiResult = CompareStudentCard(reading, info.StudentCode);
            aiDetail = JsonSerializer.Serialize(new
            {
                isStudentCard = reading?.IsStudentCard,
                fullName = reading?.FullName,
                studentCode = reading?.StudentCode,
                expectedStudentCode = info.StudentCode,
                aiAvailable = reading != null,
            });
        }

        StoredFile stored;
        using (var content = new MemoryStream(bytes))
        {
            stored = await _fileStorage.UploadImageAsync(content, request.File.FileName, StorageFolder, ct);
        }

        var photo = new BorrowHandoverPhoto
        {
            BorrowRequestId = borrowRequestId,
            PhotoType = photoType,
            PhotoUrl = stored.Url,
            StorageKey = stored.Key,
            AiResult = aiResult,
            AiDetail = aiDetail,
            TakenBy = userId,
            TakenAt = DateTime.UtcNow,
        };

        try
        {
            await _uow.Repository<BorrowHandoverPhoto>().AddAsync(photo, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            await TryDeleteStoredFileAsync(stored.Key);
            throw;
        }

        var takenByName = await _uow.Repository<User>().QueryNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.FullName)
            .FirstAsync(ct);

        return BorrowingMappings.ToHandoverPhotoResponse(photo, takenByName);
    }

    /// <summary>
    /// Hands everything over (Ready → Borrowing). Needs an Issue photo of this request; when the AI did not confirm the
    /// card the staff must give a reason. Per-unit lines get the chosen units (one line per unit), CONSUMABLE lines are
    /// taken from the pickup lab's fullest cabinets. Logged as Borrow.
    /// </summary>
    public async Task<BorrowRequestDetailResponse> IssueAsync(
        Guid borrowRequestId, IssueBorrowRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(borrowRequestId, new[] { BorrowRequestStatuses.Ready },
            "Chỉ giao đồ cho đơn đã đủ hàng ở lab nhận đồ (Ready)", ct);
        var header = await GetHeaderAsync(borrowRequestId, ct);
        await EnsurePickupStaffAsync(header.RoomId, ct);

        var photo = await _uow.Repository<BorrowHandoverPhoto>().Query()
                        .FirstOrDefaultAsync(p => p.PhotoId == request.PhotoId
                                                  && p.BorrowRequestId == borrowRequestId
                                                  && p.PhotoType == HandoverPhotoTypes.Issue, ct)
                    ?? throw new BadRequestException(
                        "Không tìm thấy ảnh giao đồ của đơn này. Hãy chụp ảnh linh kiện kèm thẻ sinh viên trước khi giao.");
        if (photo.AiResult != StudentCardCheckResults.Match)
        {
            var reason = CleanText(request.OverrideReason)
                         ?? throw new BadRequestException(
                             "AI chưa xác nhận thẻ sinh viên khớp người mượn. Hãy kiểm tra thẻ và ghi lý do để vẫn giao đồ.");
            photo.OverrideReason = reason;
        }

        await RoomStock.LockRoomAsync(_uow, header.RoomId, ct);

        var lines = await _uow.Repository<BorrowItem>().Query()
            .Include(i => i.Component)
            .Where(i => i.BorrowRequestId == borrowRequestId)
            .OrderBy(i => i.Component.ComponentCode).ThenBy(i => i.BorrowItemId)
            .ToListAsync(ct);

        var unitsByLine = request.Units.ToDictionary(u => u.BorrowItemId, u => u.ItemIds);
        foreach (var lineId in unitsByLine.Keys)
        {
            var match = lines.FirstOrDefault(l => l.BorrowItemId == lineId);
            if (match == null)
                throw new BadRequestException("Có dòng linh kiện không thuộc đơn mượn này");
            if (match.Component.TrackingType == ComponentTrackingTypes.Consumable)
                throw new BadRequestException($"{match.Component.ComponentName} là vật tư tiêu hao, không chọn từng con");
        }

        var errors = new List<string>();
        var perUnitLines = lines.Where(l => l.Component.TrackingType != ComponentTrackingTypes.Consumable).ToList();
        foreach (var line in perUnitLines)
        {
            var count = 0;
            if (unitsByLine.TryGetValue(line.BorrowItemId, out var chosen))
                count = chosen.Count;
            if (count != line.Quantity)
                errors.Add($"{line.Component.ComponentName}: cần {line.Quantity} con, đã chọn {count}");
        }
        if (errors.Count > 0)
            throw new BadRequestException("Chưa chọn đúng số con cần giao", new Dictionary<string, string[]> { ["units"] = errors.ToArray() });

        var allItemIds = unitsByLine.Values.SelectMany(ids => ids).ToList();
        var units = await LoadUnitsAsync(allItemIds, ct);
        foreach (var line in perUnitLines)
        {
            errors.AddRange(CheckUnits(units, unitsByLine[line.BorrowItemId], line.ComponentId,
                ComponentItemStatuses.Available, header.RoomId, "không nằm ở lab nhận đồ"));
        }
        if (errors.Count > 0)
            throw new BadRequestException("Có con không giao được", new Dictionary<string, string[]> { ["units"] = errors.ToArray() });

        if (allItemIds.Count > 0)
        {
            var borrowed = await _uow.Repository<ComponentItem>().Query()
                .Where(i => allItemIds.Contains(i.ItemId)
                            && i.Status == ComponentItemStatuses.Available
                            && i.Cabinet.RoomId == header.RoomId
                            && !i.IssueReports.Any(r => r.Status == IssueReportStatuses.Open))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.Status, ComponentItemStatuses.Borrowed)
                    .SetProperty(i => i.UpdatedAt, now), ct);
            if (borrowed != allItemIds.Count)
                throw new ConflictException(StockChangedMessage);
        }

        var borrowItems = _uow.Repository<BorrowItem>();
        var transactions = _uow.Repository<InventoryTransaction>();
        foreach (var line in lines)
        {
            if (line.Component.TrackingType == ComponentTrackingTypes.Consumable)
            {
                var taken = await TakeConsumableAsync(line.ComponentId, header.RoomId, line.Quantity, now, ct);
                foreach (var (cabinetId, quantity) in taken)
                    await transactions.AddAsync(NewTransaction(InventoryTransactionTypes.Borrow, line.ComponentId, cabinetId, null,
                        -quantity, borrowRequestId, header.RequestCode, userId, now), ct);

                line.Status = BorrowItemStatuses.Issued;
                line.IssuedAt = now;
                continue;
            }

            var itemIds = unitsByLine[line.BorrowItemId];
            for (var index = 0; index < itemIds.Count; index++)
            {
                var unit = units.First(u => u.ItemId == itemIds[index]);

                var target = line;
                if (index > 0)
                {
                    target = new BorrowItem
                    {
                        BorrowRequestId = line.BorrowRequestId,
                        ComponentId = line.ComponentId,
                        SourceRoomId = line.SourceRoomId,
                        ReceivedAt = line.ReceivedAt,
                        ReceivedBy = line.ReceivedBy,
                    };
                    await borrowItems.AddAsync(target, ct);
                }

                target.ComponentItemId = unit.ItemId;
                target.Quantity = 1;
                target.Status = BorrowItemStatuses.Issued;
                target.IssuedAt = now;

                await transactions.AddAsync(NewTransaction(InventoryTransactionTypes.Borrow, line.ComponentId, unit.CabinetId, unit.ItemId,
                    -1, borrowRequestId, header.RequestCode, userId, now), ct);
            }
        }

        await SaveStockChangesAsync(ct);

        await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, BorrowRequestStatuses.Borrowing), ct);

        await tx.CommitAsync(ct);

        await _notifier.IssuedAsync(borrowRequestId, ct);

        return await GetRequestByIdAsync(borrowRequestId, ct);
    }

    /// <summary>
    /// Records one return at the pickup lab with a Return photo. Per-unit: Good → Available, Damaged → Retired,
    /// Lost → Lost. A Good unit's Open issue reports are Rejected. CONSUMABLE: the quantity brought back goes into a pickup lab cabinet, the rest counts as used up.
    /// Stock back on the shelf is logged as Return. When nothing is still out and no replacement is pending the request
    /// becomes Returned.
    /// </summary>
    public async Task<BorrowRequestDetailResponse> ReturnAsync(
        Guid borrowRequestId, ReturnBorrowRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(borrowRequestId, BorrowedStatuses, "Chỉ nhận trả cho đơn đang mượn", ct);
        var header = await GetHeaderAsync(borrowRequestId, ct);
        await EnsurePickupStaffAsync(header.RoomId, ct);

        var photoOk = await _uow.Repository<BorrowHandoverPhoto>().QueryNoTracking()
            .AnyAsync(p => p.PhotoId == request.PhotoId
                           && p.BorrowRequestId == borrowRequestId
                           && p.PhotoType == HandoverPhotoTypes.Return, ct);
        if (!photoOk)
            throw new BadRequestException("Không tìm thấy ảnh nhận trả của đơn này. Hãy chụp ảnh đồ trả về trước khi nhận.");

        var lineIds = request.Lines.Select(l => l.BorrowItemId).ToList();
        var lines = await _uow.Repository<BorrowItem>().Query()
            .Include(i => i.Component)
            .Where(i => i.BorrowRequestId == borrowRequestId && lineIds.Contains(i.BorrowItemId))
            .ToListAsync(ct);
        if (lines.Count != lineIds.Count)
            throw new BadRequestException("Có dòng linh kiện không thuộc đơn mượn này");

        var errors = new List<string>();
        foreach (var line in lines)
        {
            var input = request.Lines.First(l => l.BorrowItemId == line.BorrowItemId);
            if (line.Status == BorrowItemStatuses.Pending)
                errors.Add($"{line.Component.ComponentName}: món thay thế chưa giao nên chưa trả được");
            else if (line.Status != BorrowItemStatuses.Issued)
                errors.Add($"{line.Component.ComponentName}: dòng này đã được trả");
            else if (line.Component.TrackingType == ComponentTrackingTypes.Consumable)
            {
                if (input.ReturnedQuantity == null || input.ReturnedQuantity > line.Quantity)
                    errors.Add($"{line.Component.ComponentName}: số lượng trả phải từ 0 đến {line.Quantity}");
            }
            else if (ReturnConditions.Normalize(input.Condition) == null)
            {
                errors.Add($"{line.Component.ComponentName}: phải chọn tình trạng {string.Join(" / ", ReturnConditions.All)}");
            }
        }
        if (errors.Count > 0)
            throw new BadRequestException("Thông tin trả chưa hợp lệ", new Dictionary<string, string[]> { ["lines"] = errors.ToArray() });

        var consumableBack = lines
            .Where(l => l.Component.TrackingType == ComponentTrackingTypes.Consumable)
            .Sum(l => request.Lines.First(r => r.BorrowItemId == l.BorrowItemId).ReturnedQuantity!.Value);
        if (consumableBack > 0)
        {
            var cabinetOk = request.CabinetId.HasValue
                            && await _uow.Repository<StorageCabinet>().QueryNoTracking()
                                .AnyAsync(c => c.CabinetId == request.CabinetId.Value
                                               && c.RoomId == header.RoomId
                                               && c.Status == CabinetStatuses.Active, ct);
            if (!cabinetOk)
                throw new BadRequestException("Phải chọn tủ đang hoạt động của lab nhận đồ để cất vật tư tiêu hao trả về");
        }

        var unitIds = lines.Where(l => l.ComponentItemId != null).Select(l => l.ComponentItemId!.Value).ToList();
        var unitCabinets = await _uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => unitIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, i => i.CabinetId, ct);

        var transactions = _uow.Repository<InventoryTransaction>();
        foreach (var line in lines)
        {
            var input = request.Lines.First(l => l.BorrowItemId == line.BorrowItemId);

            if (line.Component.TrackingType == ComponentTrackingTypes.Consumable)
            {
                line.ReturnedAt = now;
                var quantity = input.ReturnedQuantity!.Value;
                if (quantity > 0)
                {
                    await AddConsumableAsync(line.ComponentId, request.CabinetId!.Value, quantity, now, ct);
                    await transactions.AddAsync(NewTransaction(InventoryTransactionTypes.Return, line.ComponentId, request.CabinetId.Value, null,
                        quantity, borrowRequestId, header.RequestCode, userId, now), ct);
                }

                line.Status = BorrowItemStatuses.Returned;
                line.ReturnedQuantity = quantity;
                line.ReturnPhotoId = request.PhotoId;
                continue;
            }

            var condition = ReturnConditions.Normalize(input.Condition)!;
            var unitId = line.ComponentItemId!.Value;

            if (condition == ReturnConditions.Good)
            {
                var updated = await _uow.Repository<ComponentItem>().Query()
                    .Where(i => i.ItemId == unitId && i.Status == ComponentItemStatuses.Borrowed)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(i => i.Status, ComponentItemStatuses.Available)
                        .SetProperty(i => i.UpdatedAt, now), ct);
                if (updated == 0)
                    throw new ConflictException(StockChangedMessage);

                await transactions.AddAsync(NewTransaction(InventoryTransactionTypes.Return, line.ComponentId, unitCabinets[unitId], unitId,
                    1, borrowRequestId, header.RequestCode, userId, now), ct);
                await _issueReports.RejectOpenReportsOnGoodReturnAsync(line.BorrowItemId, ct);

                line.Status = BorrowItemStatuses.Returned;
                line.ReturnedQuantity = 1;
                line.ReturnPhotoId = request.PhotoId;
            }
            else
            {
                await _issueReports.ConfirmReturnIssueAsync(new ConfirmBorrowingReturnIssueCommand
                {
                    BorrowItemId = line.BorrowItemId,
                    IssueType = condition,
                    Description = ReturnIssueDescription(condition, header.RequestCode),
                }, ct);

                if (condition == ReturnConditions.Damaged)
                {
                    line.Status = BorrowItemStatuses.Damaged;
                    line.ReturnedQuantity = 1;
                    line.ReturnPhotoId = request.PhotoId;
                }
                else
                {
                    line.Status = BorrowItemStatuses.Lost;
                    line.ReturnedQuantity = 0;
                    line.ReturnPhotoId = null;
                }
            }

            line.ReturnedAt = now;
            line.ReturnCondition = condition;
        }

        await SaveStockChangesAsync(ct);
        await CloseWhenAllBackAsync(borrowRequestId, now, ct);

        await tx.CommitAsync(ct);

        await _notifier.ReturnedAsync(borrowRequestId, lineIds, ct);

        return await GetRequestByIdAsync(borrowRequestId, ct);
    }

    /// <summary>
    /// Swaps a unit that broke during the loan (request being borrowed), with a Return photo of it. The broken unit is
    /// confirmed as Damaged through Issue Reports (unit Retired, an Open report of it Confirmed) and its line closed. Then
    /// a Pending replacement line of the same component is added, taken from the pickup lab when it has a free unit
    /// (ready to hand over), otherwise from the lab with the most free units (to carry over first). When no lab has one
    /// left there is no replacement and the request becomes Returned if nothing else is out. Every lab is locked first,
    /// like approving, so the free unit cannot be promised twice.
    /// </summary>
    public async Task<ReplaceDamagedResponse> ReplaceDamagedAsync(
        Guid borrowRequestId, ReplaceDamagedRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(borrowRequestId, BorrowedStatuses, "Chỉ đổi món hỏng cho đơn đang mượn", ct);
        var header = await GetHeaderAsync(borrowRequestId, ct);
        await EnsurePickupStaffAsync(header.RoomId, ct);

        var photoOk = await _uow.Repository<BorrowHandoverPhoto>().QueryNoTracking()
            .AnyAsync(p => p.PhotoId == request.PhotoId
                           && p.BorrowRequestId == borrowRequestId
                           && p.PhotoType == HandoverPhotoTypes.Return, ct);
        if (!photoOk)
            throw new BadRequestException("Không tìm thấy ảnh nhận trả của đơn này. Hãy chụp ảnh con bị hỏng trước khi đổi.");

        var line = await _uow.Repository<BorrowItem>().Query()
                       .Include(i => i.Component)
                       .FirstOrDefaultAsync(i => i.BorrowItemId == request.BorrowItemId && i.BorrowRequestId == borrowRequestId, ct)
                   ?? throw new NotFoundException("Không tìm thấy dòng linh kiện trong đơn mượn");
        if (line.Component.TrackingType == ComponentTrackingTypes.Consumable || line.ComponentItemId == null)
            throw new BadRequestException("Chỉ đổi được hàng theo từng con, vật tư tiêu hao không đổi");
        if (line.Status != BorrowItemStatuses.Issued)
            throw new ConflictException($"{line.Component.ComponentName}: dòng này không còn đang mượn");

        await RoomStock.LockAllRoomsAsync(_uow, ct);

        await _issueReports.ConfirmReturnIssueAsync(new ConfirmBorrowingReturnIssueCommand
        {
            BorrowItemId = line.BorrowItemId,
            IssueType = ReturnConditions.Damaged,
            Description = $"Hỏng trong lúc mượn, đổi con khác cho đơn {header.RequestCode}",
        }, ct);

        line.Status = BorrowItemStatuses.Damaged;
        line.ReturnedQuantity = 1;
        line.ReturnPhotoId = request.PhotoId;
        line.ReturnedAt = now;
        line.ReturnCondition = ReturnConditions.Damaged;

        var sourceRoomId = await PickReplacementRoomAsync(line.ComponentId, header.RoomId, ct);
        BorrowItem? replacement = null;
        if (sourceRoomId != null)
        {
            replacement = new BorrowItem
            {
                BorrowRequestId = borrowRequestId,
                ComponentId = line.ComponentId,
                Status = BorrowItemStatuses.Pending,
                Quantity = 1,
                SourceRoomId = sourceRoomId,
            };
            if (sourceRoomId == header.RoomId)
            {
                replacement.ReceivedAt = now;
                replacement.ReceivedBy = userId;
            }
            await _uow.Repository<BorrowItem>().AddAsync(replacement, ct);
        }

        await SaveStockChangesAsync(ct);
        await CloseWhenAllBackAsync(borrowRequestId, now, ct);

        await tx.CommitAsync(ct);

        await _notifier.ReplacementAsync(borrowRequestId, line.BorrowItemId, replacement?.BorrowItemId, ct);

        var response = new ReplaceDamagedResponse
        {
            Request = await GetRequestByIdAsync(borrowRequestId, ct),
        };
        if (replacement != null)
        {
            response.ReplacementBorrowItemId = replacement.BorrowItemId;
            response.SourceRoomId = replacement.SourceRoomId;
            response.SourceRoomCode = await _uow.Repository<LabRoom>().QueryNoTracking()
                .Where(r => r.RoomId == replacement.SourceRoomId)
                .Select(r => r.RoomCode)
                .FirstAsync(ct);
            response.WaitingTransfer = replacement.ReceivedAt == null;
        }

        return response;
    }

    /// <summary>
    /// Hands a replacement unit over: the line must be at the pickup lab, with an Issue photo taken after it got there
    /// (the staff gives a reason when the AI did not confirm the card). The unit becomes Borrowed, logged as Borrow.
    /// The due date does not change.
    /// </summary>
    public async Task<BorrowRequestDetailResponse> IssueReplacementAsync(
        Guid borrowRequestId, IssueReplacementRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(borrowRequestId, BorrowedStatuses, "Chỉ giao món thay thế cho đơn đang mượn", ct);
        var header = await GetHeaderAsync(borrowRequestId, ct);
        await EnsurePickupStaffAsync(header.RoomId, ct);

        var line = await FindReplacementAsync(borrowRequestId, request.BorrowItemId, ct);
        if (line.ReceivedAt == null)
            throw new ConflictException("Món thay thế chưa được chuyển về lab nhận đồ");

        var photo = await _uow.Repository<BorrowHandoverPhoto>().Query()
                        .FirstOrDefaultAsync(p => p.PhotoId == request.PhotoId
                                                  && p.BorrowRequestId == borrowRequestId
                                                  && p.PhotoType == HandoverPhotoTypes.Issue
                                                  && p.TakenAt >= line.ReceivedAt, ct)
                    ?? throw new BadRequestException(
                        "Không tìm thấy ảnh giao món thay thế. Hãy chụp ảnh con thay thế kèm thẻ sinh viên trước khi giao.");
        if (photo.AiResult != StudentCardCheckResults.Match)
        {
            var reason = CleanText(request.OverrideReason)
                         ?? throw new BadRequestException(
                             "AI chưa xác nhận thẻ sinh viên khớp người mượn. Hãy kiểm tra thẻ và ghi lý do để vẫn giao đồ.");
            photo.OverrideReason = reason;
        }

        await RoomStock.LockRoomAsync(_uow, header.RoomId, ct);

        var itemIds = new List<Guid> { request.ItemId };
        var units = await LoadUnitsAsync(itemIds, ct);
        var errors = CheckUnits(units, itemIds, line.ComponentId, ComponentItemStatuses.Available, header.RoomId,
            "không nằm ở lab nhận đồ");
        if (errors.Count > 0)
            throw new BadRequestException("Con này không giao được", new Dictionary<string, string[]> { ["itemId"] = errors.ToArray() });

        var borrowed = await _uow.Repository<ComponentItem>().Query()
            .Where(i => i.ItemId == request.ItemId
                        && i.Status == ComponentItemStatuses.Available
                        && i.Cabinet.RoomId == header.RoomId
                        && !i.IssueReports.Any(r => r.Status == IssueReportStatuses.Open))
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, ComponentItemStatuses.Borrowed)
                .SetProperty(i => i.UpdatedAt, now), ct);
        if (borrowed == 0)
            throw new ConflictException(StockChangedMessage);

        line.ComponentItemId = request.ItemId;
        line.Status = BorrowItemStatuses.Issued;
        line.IssuedAt = now;

        var unit = units[0];
        await _uow.Repository<InventoryTransaction>().AddAsync(NewTransaction(InventoryTransactionTypes.Borrow, line.ComponentId,
            unit.CabinetId, unit.ItemId, -1, borrowRequestId, header.RequestCode, userId, now), ct);

        await SaveStockChangesAsync(ct);

        await tx.CommitAsync(ct);

        await _notifier.ReplacementIssuedAsync(borrowRequestId, line.BorrowItemId, ct);

        return await GetRequestByIdAsync(borrowRequestId, ct);
    }

    /// <summary>
    /// Drops a replacement not handed over yet, with a reason emailed to the borrower (e.g. the source lab found its unit
    /// broken too). A unit already carried to the pickup lab stays there. The request becomes Returned if nothing else is
    /// out.
    /// </summary>
    public async Task<BorrowRequestDetailResponse> CancelReplacementAsync(
        Guid borrowRequestId, CancelReplacementRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(borrowRequestId, BorrowedStatuses, "Chỉ hủy món thay thế của đơn đang mượn", ct);
        var header = await GetHeaderAsync(borrowRequestId, ct);
        await EnsurePickupStaffAsync(header.RoomId, ct);

        var line = await FindReplacementAsync(borrowRequestId, request.BorrowItemId, ct);
        var componentName = line.Component.ComponentName;
        Guid? notCarriedFrom = null;
        if (line.ReceivedAt == null)
            notCarriedFrom = line.SourceRoomId;

        _uow.Repository<BorrowItem>().Remove(line);
        await _uow.SaveChangesAsync(ct);
        await CloseWhenAllBackAsync(borrowRequestId, now, ct);

        await tx.CommitAsync(ct);

        await _notifier.ReplacementCancelledAsync(borrowRequestId, componentName, notCarriedFrom, request.Reason.Trim(), userId, ct);

        return await GetRequestByIdAsync(borrowRequestId, ct);
    }

    /// <summary>
    /// Cancels an Approved or Ready request (nothing handed over yet): the borrower, staff of the pickup lab or an Admin.
    /// Staff must give a reason (e.g. a source lab found components broken or lost after approval); it is saved in
    /// RejectReason and emailed to the borrower. The reservation ends; components already carried to the pickup lab stay
    /// there. The project goes back to Draft so the team can change it and submit again.
    /// </summary>
    public async Task<BorrowRequestDetailResponse> CancelAsync(
        Guid borrowRequestId, CancelBorrowRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var info = await _uow.Repository<BorrowRequest>().QueryNoTracking()
                       .Where(r => r.BorrowRequestId == borrowRequestId)
                       .Select(r => new { r.RequesterId, r.RoomId, r.ProjectId })
                       .FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Không tìm thấy đơn mượn");

        var byBorrower = info.RequesterId == userId;
        if (!byBorrower && !await IsPickupStaffAsync(info.RoomId, ct))
            throw new ForbiddenException("Chỉ người mượn hoặc nhân viên lab nhận đồ được hủy đơn này");

        var reason = CleanText(request.Reason);
        if (!byBorrower && reason == null)
            throw new BadRequestException("Nhân viên hủy đơn phải ghi lý do để gửi cho sinh viên");

        var now = DateTime.UtcNow;
        await using var tx = await _uow.BeginTransactionAsync(ct);

        var cancelled = await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.BorrowRequestId == borrowRequestId && RoomStock.ReservingStatuses.Contains(r.Status))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, BorrowRequestStatuses.Cancelled)
                .SetProperty(r => r.RejectReason, reason), ct);
        if (cancelled == 0)
            throw new ConflictException("Chỉ hủy được đơn đã duyệt mà chưa giao đồ");

        if (info.ProjectId.HasValue)
        {
            await _uow.Repository<Project>().Query()
                .Where(p => p.ProjectId == info.ProjectId.Value && p.Status == ProjectStatuses.Approved)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, ProjectStatuses.Draft)
                    .SetProperty(p => p.SubmittedAt, (DateTime?)null)
                    .SetProperty(p => p.UpdatedAt, now), ct);
        }

        await tx.CommitAsync(ct);

        if (!byBorrower)
            await _notifier.CancelledByStaffAsync(borrowRequestId, ct);
        await _notifier.StaffCancelledAsync(borrowRequestId, userId, ct);

        return await GetRequestByIdAsync(borrowRequestId, ct);
    }

    private sealed record RequestHeader(Guid RoomId, string RequestCode, string Status);

    private Task<RequestHeader> GetHeaderAsync(Guid borrowRequestId, CancellationToken ct)
        => _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .Select(r => new RequestHeader(r.RoomId, r.RequestCode, r.Status))
            .FirstAsync(ct);

    /// <summary>
    /// Once no line is Issued and no replacement is Pending, the request becomes Returned and its Pending extensions are
    /// rejected. Call inside the caller's transaction after saving the line changes.
    /// </summary>
    private async Task CloseWhenAllBackAsync(Guid borrowRequestId, DateTime now, CancellationToken ct)
    {
        var stillOut = await _uow.Repository<BorrowItem>().QueryNoTracking()
            .AnyAsync(i => i.BorrowRequestId == borrowRequestId
                           && (i.Status == BorrowItemStatuses.Issued || i.Status == BorrowItemStatuses.Pending), ct);
        if (stillOut)
            return;

        await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, BorrowRequestStatuses.Returned)
                .SetProperty(r => r.ReturnedAt, now), ct);

        await _uow.Repository<BorrowExtension>().Query()
            .Where(e => e.BorrowRequestId == borrowRequestId && e.Status == BorrowExtensionStatuses.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, BorrowExtensionStatuses.Rejected)
                .SetProperty(e => e.ReviewComment, "Đã trả hết đồ, không cần gia hạn")
                .SetProperty(e => e.ReviewedAt, now), ct);
    }

    /// <summary>
    /// Lab to take a replacement unit from: the pickup lab when it has a free one, otherwise the lab with the most free
    /// (ties by lab id); null when no lab has one. Free means not reserved (see RoomStock). Labs must already be locked.
    /// </summary>
    private async Task<Guid?> PickReplacementRoomAsync(Guid componentId, Guid pickupRoomId, CancellationToken ct)
    {
        var byRoom = await RoomStock.GetAvailableByRoomAsync(_uow, new[] { componentId }, ct);

        if (byRoom.TryGetValue(pickupRoomId, out var pickup) && pickup.GetValueOrDefault(componentId) > 0)
            return pickupRoomId;

        return byRoom
            .Where(r => r.Value.GetValueOrDefault(componentId) > 0)
            .OrderByDescending(r => r.Value[componentId]).ThenBy(r => r.Key)
            .Select(r => (Guid?)r.Key)
            .FirstOrDefault();
    }

    /// <summary>A Pending replacement line of the request (tracked, with its component); otherwise 404 / 409.</summary>
    private async Task<BorrowItem> FindReplacementAsync(Guid borrowRequestId, Guid borrowItemId, CancellationToken ct)
    {
        var line = await _uow.Repository<BorrowItem>().Query()
                       .Include(i => i.Component)
                       .FirstOrDefaultAsync(i => i.BorrowItemId == borrowItemId && i.BorrowRequestId == borrowRequestId, ct)
                   ?? throw new NotFoundException("Không tìm thấy dòng linh kiện trong đơn mượn");
        if (line.Status != BorrowItemStatuses.Pending)
            throw new ConflictException("Dòng này không phải món thay thế đang chờ giao");

        return line;
    }

    /// <summary>Locks the request row while it is in one of <paramref name="statuses"/>; otherwise 404 / 409.</summary>
    private async Task LockRequestAsync(Guid borrowRequestId, string[] statuses, string wrongStatusMessage, CancellationToken ct)
    {
        var locked = await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.BorrowRequestId == borrowRequestId && statuses.Contains(r.Status))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, r => r.Status), ct);
        if (locked > 0)
            return;

        if (!await _uow.Repository<BorrowRequest>().QueryNoTracking().AnyAsync(r => r.BorrowRequestId == borrowRequestId, ct))
            throw new NotFoundException("Không tìm thấy đơn mượn");

        throw new ConflictException(wrongStatusMessage);
    }

    /// <summary>Admin, or a staff member assigned to the pickup lab (lab_room_staff).</summary>
    private static string ReturnIssueDescription(string condition, string requestCode)
    {
        if (condition == ReturnConditions.Lost)
            return $"Báo mất khi nhận trả đơn {requestCode}";

        return $"Phát hiện hỏng khi nhận trả đơn {requestCode}";
    }

    private async Task EnsurePickupStaffAsync(Guid roomId, CancellationToken ct)
    {
        if (!await IsPickupStaffAsync(roomId, ct))
            throw new ForbiddenException("Chỉ nhân viên phụ trách lab nhận đồ được thao tác với đơn này");
    }

    private async Task<bool> IsPickupStaffAsync(Guid roomId, CancellationToken ct)
    {
        if (_currentUser.IsInRole(RoleNames.Admin))
            return true;

        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        return await _uow.Repository<LabRoomStaff>().QueryNoTracking()
            .AnyAsync(s => s.RoomId == roomId && s.UserId == userId, ct);
    }

    private IQueryable<Guid> MyRoomIds()
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        return _uow.Repository<LabRoomStaff>().QueryNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.RoomId);
    }

    /// <summary>
    /// Admin sees every request; others see their own and their team's, their classes', the ones they approve, and
    /// (staff) those of the labs they are assigned to.
    /// </summary>
    private IQueryable<BorrowRequest> VisibleRequests()
    {
        var requests = _uow.Repository<BorrowRequest>().QueryNoTracking();
        if (_currentUser.IsInRole(RoleNames.Admin))
            return requests;

        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var isStaff = _currentUser.HasPermission(PermissionCodes.BorrowIssue);
        var myRooms = MyRoomIds();

        return requests.Where(r =>
            r.RequesterId == userId
            || (r.Project != null && r.Project.Team != null
                && (r.Project.Team.Leader.UserId == userId
                    || r.Project.Team.TeamMembers.Any(tm => tm.Student.UserId == userId
                                                            && tm.Status == TeamMemberStatuses.Accepted)))
            || (r.Class != null && r.Class.Instructor != null && r.Class.Instructor.UserId == userId)
            || r.BorrowApprovals.Any(a => a.ApproverId == userId)
            || (isStaff && (myRooms.Contains(r.RoomId)
                            || r.BorrowItems.Any(i => i.SourceRoomId != null && myRooms.Contains(i.SourceRoomId.Value)))));
    }

    private static IQueryable<BorrowRequest> Filter(IQueryable<BorrowRequest> requests, BorrowRequestQueryParams query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            requests = requests.Where(r => r.RequestCode.ToLower().Contains(keyword)
                                           || r.Requester.FullName.ToLower().Contains(keyword)
                                           || (r.Project != null && r.Project.Title.ToLower().Contains(keyword)));
        }

        var status = BorrowRequestStatuses.Normalize(query.Status);
        if (status != null)
            requests = requests.Where(r => r.Status == status);

        if (query.Overdue == true)
        {
            var overdueBefore = OverdueBefore();
            requests = requests.Where(r => BorrowedStatuses.Contains(r.Status) && r.DueDate < overdueBefore);
        }

        return requests;
    }

    /// <summary>
    /// due_date is the start (00:00 Vietnam time) of the last day to return, so a request is overdue only once that whole
    /// day has passed: due_date is before this moment.
    /// </summary>
    private static DateTime OverdueBefore() => DateTime.UtcNow.AddDays(-1);

    private static Task<PagedResult<BorrowRequestResponse>> ToPagedResponses(
        IQueryable<BorrowRequest> requests, PageParams paging, CancellationToken ct)
    {
        var overdueBefore = OverdueBefore();
        var result = requests
            .OrderByDescending(r => r.RequestDate).ThenBy(r => r.BorrowRequestId)
            .Select(r => new BorrowRequestResponse
            {
                BorrowRequestId = r.BorrowRequestId,
                RequestCode = r.RequestCode,
                Status = r.Status,
                IsOverdue = BorrowedStatuses.Contains(r.Status) && r.DueDate < overdueBefore,
                RequesterId = r.RequesterId,
                RequesterName = r.Requester.FullName,
                ClassId = r.ClassId,
                ClassCode = r.Class != null ? r.Class.ClassCode : null,
                ProjectId = r.ProjectId,
                ProjectTitle = r.Project != null ? r.Project.Title : null,
                RoomId = r.RoomId,
                RoomCode = r.Room.RoomCode,
                RoomName = r.Room.RoomName,
                RequestDate = r.RequestDate,
                ApprovedAt = r.ApprovedAt,
                BorrowDate = r.BorrowDate,
                DueDate = r.DueDate,
                ReturnedAt = r.ReturnedAt,
                ItemCount = r.BorrowItems.Count,
                WaitingTransferCount = r.BorrowItems.Count(i => i.SourceRoomId != null && i.ReceivedAt == null),
            });

        return PagedResult<BorrowRequestResponse>.CreateAsync(result, paging, ct);
    }

    private sealed record UnitRow(Guid ItemId, string ItemCode, Guid ComponentId, string Status, Guid CabinetId, Guid RoomId,
        bool HasOpenIssue);

    private Task<List<UnitRow>> LoadUnitsAsync(List<Guid> itemIds, CancellationToken ct)
        => _uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => itemIds.Contains(i.ItemId))
            .Select(i => new UnitRow(i.ItemId, i.ItemCode, i.ComponentId, i.Status, i.CabinetId, i.Cabinet.RoomId,
                i.IssueReports.Any(r => r.Status == IssueReportStatuses.Open)))
            .ToListAsync(ct);

    /// <summary>
    /// Error messages for chosen units that are unknown, of another component, in the wrong status or lab, or with an Open
    /// issue report.
    /// </summary>
    private static List<string> CheckUnits(
        List<UnitRow> units, List<Guid> chosen, Guid componentId, string status, Guid roomId, string wrongRoomText)
    {
        var errors = new List<string>();
        foreach (var itemId in chosen)
        {
            var unit = units.FirstOrDefault(u => u.ItemId == itemId);
            if (unit == null)
                errors.Add($"Không tìm thấy con có id {itemId}");
            else if (unit.ComponentId != componentId)
                errors.Add($"{unit.ItemCode}: không phải linh kiện của dòng này");
            else if (unit.Status != status)
                errors.Add($"{unit.ItemCode}: đang ở trạng thái {unit.Status}");
            else if (unit.RoomId != roomId)
                errors.Add($"{unit.ItemCode}: {wrongRoomText}");
            else if (unit.HasOpenIssue)
                errors.Add($"{unit.ItemCode}: đang có báo cáo sự cố chờ xử lý");
        }

        return errors;
    }

    /// <summary>
    /// Takes a CONSUMABLE quantity out of a lab, fullest cabinets first. Each cabinet update only succeeds while it still
    /// holds enough, so a parallel change gives 409 instead of negative stock.
    /// </summary>
    private async Task<List<(Guid CabinetId, int Quantity)>> TakeConsumableAsync(
        Guid componentId, Guid roomId, int quantity, DateTime now, CancellationToken ct)
    {
        var stocks = _uow.Repository<ConsumableStock>();
        var rows = await stocks.QueryNoTracking()
            .Where(s => s.ComponentId == componentId && s.Cabinet.RoomId == roomId && s.Quantity > 0)
            .OrderByDescending(s => s.Quantity).ThenBy(s => s.CabinetId)
            .Select(s => new { s.CabinetId, s.Quantity })
            .ToListAsync(ct);

        var taken = new List<(Guid CabinetId, int Quantity)>();
        var left = quantity;
        foreach (var row in rows)
        {
            if (left == 0)
                break;

            var take = Math.Min(left, row.Quantity);
            var updated = await stocks.Query()
                .Where(s => s.ComponentId == componentId && s.CabinetId == row.CabinetId && s.Quantity >= take)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(st => st.Quantity, st => st.Quantity - take)
                    .SetProperty(st => st.UpdatedAt, now), ct);
            if (updated == 0)
                throw new ConflictException(StockChangedMessage);

            taken.Add((row.CabinetId, take));
            left -= take;
        }

        if (left > 0)
            throw new ConflictException($"Lab chỉ còn {quantity - left}, không đủ {quantity}. {StockChangedMessage}");

        return taken;
    }

    private async Task AddConsumableAsync(Guid componentId, Guid cabinetId, int quantity, DateTime now, CancellationToken ct)
    {
        var stocks = _uow.Repository<ConsumableStock>();
        var added = await stocks.Query()
            .Where(s => s.ComponentId == componentId && s.CabinetId == cabinetId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(st => st.Quantity, st => st.Quantity + quantity)
                .SetProperty(st => st.UpdatedAt, now), ct);
        if (added > 0)
            return;

        await stocks.AddAsync(new ConsumableStock
        {
            ComponentId = componentId,
            CabinetId = cabinetId,
            Quantity = quantity,
            UpdatedAt = now,
        }, ct);
    }

    private static InventoryTransaction NewTransaction(
        string type, Guid componentId, Guid cabinetId, Guid? itemId, int quantityChange,
        Guid borrowRequestId, string requestCode, Guid userId, DateTime now) => new()
    {
        Type = type,
        ComponentId = componentId,
        CabinetId = cabinetId,
        ItemId = itemId,
        QuantityChange = quantityChange,
        Reason = $"Đơn mượn {requestCode}",
        BorrowRequestId = borrowRequestId,
        CreatedBy = userId,
        CreatedAt = now,
    };

    /// <summary>Student code on the card vs the borrower's (spaces and case ignored).</summary>
    private static string CompareStudentCard(StudentCardReading? reading, string? expectedStudentCode)
    {
        if (reading == null || !reading.IsStudentCard
                            || string.IsNullOrWhiteSpace(reading.StudentCode)
                            || string.IsNullOrWhiteSpace(expectedStudentCode))
            return StudentCardCheckResults.Unreadable;

        if (NormalizeCode(reading.StudentCode) == NormalizeCode(expectedStudentCode))
            return StudentCardCheckResults.Match;

        return StudentCardCheckResults.Mismatch;
    }

    private static string NormalizeCode(string code) => code.Replace(" ", string.Empty).Trim().ToUpperInvariant();

    private async Task SaveStockChangesAsync(CancellationToken ct)
    {
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException(StockChangedMessage);
        }
    }

    private async Task TryDeleteStoredFileAsync(string key)
    {
        try
        {
            await _fileStorage.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete orphan handover photo {Key}", key);
        }
    }

    private static string? CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Trim();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;
using SmartLab.BLL.DTOs.IssueReports;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.External.Storage;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.BLL.Interfaces.IssueReports;
using SmartLab.BLL.Mappings.IssueReports;
using SmartLab.BLL.Services.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.IssueReports;

public class IssueReportService : IIssueReportService
{
    private const string ComponentItemAvailable = "Available";
    private const string ComponentItemBorrowed = "Borrowed";
    private const string BorrowItemIssued = "Issued";
    private const string BorrowItemReturned = "Returned";
    private const string ReturnConditionGood = "Good";
    private const string BorrowRequestBorrowing = "Borrowing";
    private const string BorrowRequestOverdue = "Overdue";
    private const string TrackingReusable = "REUSABLE";
    private const string TrackingHighValue = "HIGH_VALUE";
    private const string IssueImageFolder = "issues";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorage _fileStorage;
    private readonly IBorrowRequestService _borrowRequests;
    private readonly ILogger<IssueReportService> _logger;

    public IssueReportService(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IFileStorage fileStorage,
        IBorrowRequestService borrowRequests,
        ILogger<IssueReportService> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _borrowRequests = borrowRequests;
        _logger = logger;
    }

    /// <summary>
    /// The unit with this item code or serial, and who is borrowing it. Fails with the same errors as
    /// <see cref="CreateAsync"/>, so the form tells them right after scanning.
    /// </summary>
    public async Task<IssueReportLookupResponse> LookupAsync(string? code, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var keyword = code?.Trim().ToLower();
        if (string.IsNullOrWhiteSpace(keyword))
            throw new BadRequestException("Nhập hoặc quét mã thiết bị");

        var item = await ItemQuery()
                       .Where(candidate => candidate.ItemCode.ToLower() == keyword ||
                                           (candidate.SerialNumber != null && candidate.SerialNumber.ToLower() == keyword))
                       .OrderByDescending(candidate => candidate.Status == ComponentItemAvailable ||
                                                       candidate.Status == ComponentItemBorrowed)
                       .ThenByDescending(candidate => candidate.ImportedAt)
                       .FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Không tìm thấy thiết bị có mã hoặc serial này");

        var activeBorrowItem = await EnsureCanReportAsync(item, userId, ct);
        var freeUnits = 0;
        if (activeBorrowItem is not null)
        {
            var byRoom = await RoomStock.GetAvailableByRoomAsync(_uow, new[] { item.ComponentId }, ct);
            if (byRoom.TryGetValue(activeBorrowItem.BorrowRequest.RoomId, out var room))
                freeUnits = room.GetValueOrDefault(item.ComponentId);
        }

        return new IssueReportLookupResponse
        {
            Item = IssueReportMappings.ToItemResponse(item)!,
            BorrowContext = IssueReportMappings.ToBorrowContextResponse(activeBorrowItem),
            ConfirmsImmediately = await CanConfirmAsync(item, userId, ct),
            FreeReplacementUnits = freeUnits,
        };
    }

    /// <summary>
    /// Lab staff have the unit in hand, so their report is Confirmed at once: a lent unit closes its borrow line (with a
    /// replacement when asked for), a unit in its cabinet leaves the stock. An inventory manager's report stays Open until
    /// the lab staff check it.
    /// </summary>
    public async Task<IssueReportDetailResponse> CreateAsync(
        CreateIssueReportRequest request,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var issueType = IssueTypes.Normalize(request.IssueType)
                        ?? throw new BadRequestException("Loại sự cố phải là Damaged, Burnt hoặc Lost");
        var description = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description))
            throw new BadRequestException("Mô tả sự cố là bắt buộc");
        if (description.Length > 1000)
            throw new BadRequestException("Mô tả sự cố không được vượt quá 1000 ký tự");

        var item = await FindItemAsync(request.ComponentItemId, request.ItemCode, ct);
        var activeBorrowItem = await EnsureCanReportAsync(item, userId, ct);
        if (request.Replace && activeBorrowItem is null)
            throw new BadRequestException("Chỉ đổi con khác khi thiết bị đang được mượn");

        var confirmNow = await CanConfirmAsync(item, userId, ct);
        if (activeBorrowItem is not null && !confirmNow)
            throw new ForbiddenException("Chỉ LabStaff phụ trách đúng lab hoặc Admin được ghi nhận thiết bị đang mượn");

        var image = await UploadImageAsync(request, ct);
        Guid reportId;
        Guid? replacementId = null;
        try
        {
            if (activeBorrowItem is not null)
            {
                var recorded = await _borrowRequests.RecordLoanIssueAsync(new RecordLoanIssueCommand
                {
                    BorrowItemId = activeBorrowItem.BorrowItemId,
                    IssueType = issueType,
                    Description = description,
                    ImageUrl = image?.Url,
                    Replace = request.Replace,
                }, ct);
                reportId = recorded.IssueReportId;
                replacementId = recorded.ReplacementBorrowItemId;
            }
            else
            {
                reportId = await CreateForCabinetItemAsync(item, issueType, description, image?.Url, confirmNow, userId, ct);
            }
        }
        catch
        {
            if (image is not null)
                await DeleteUnusedImageAsync(image);

            throw;
        }

        var response = await LoadDetailAsync(reportId, ct);
        response.ReplacementBorrowItemId = replacementId;
        return response;
    }

    /// <summary>Saves the report of a unit in its cabinet, confirmed in the same transaction when <paramref name="confirmNow"/>.</summary>
    private async Task<Guid> CreateForCabinetItemAsync(
        ComponentItem item,
        string issueType,
        string description,
        string? imageUrl,
        bool confirmNow,
        Guid userId,
        CancellationToken ct)
    {
        await using var transaction = await _uow.BeginTransactionAsync(ct);

        var affectedItems = await _uow.Repository<ComponentItem>().Query()
            .Where(candidate => candidate.ItemId == item.ItemId &&
                                candidate.Status == ComponentItemAvailable)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.Status, candidate => candidate.Status), ct);
        if (affectedItems != 1)
            throw new ConflictException("Trạng thái linh kiện đã thay đổi, vui lòng tải lại dữ liệu");

        item = await FindItemByIdAsync(item.ItemId, ct);
        await EnsureCanReportAsync(item, userId, ct);

        var now = DateTime.UtcNow;
        var report = new IssueReport
        {
            IssueReportId = Guid.NewGuid(),
            ComponentItemId = item.ItemId,
            ReportedBy = userId,
            IssueType = issueType,
            Description = description,
            ImageUrl = imageUrl,
            Status = IssueReportStatuses.Open,
            CreatedAt = now,
        };
        if (confirmNow)
        {
            report.Status = IssueReportStatuses.Confirmed;
            report.ResolvedBy = userId;
            report.ResolvedAt = now;
        }

        await _uow.Repository<IssueReport>().AddAsync(report, ct);
        if (confirmNow)
        {
            await IssueReportConfirmationHandler.ConfirmAsync(_uow, new IssueReport
            {
                IssueReportId = report.IssueReportId,
                ComponentItemId = item.ItemId,
                IssueType = issueType,
                ComponentItem = item,
            }, userId, null, now, ct);
        }

        await _uow.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return report.IssueReportId;
    }

    /// <summary>Checks that the user may report this unit now; returns its active borrow line (null when in its cabinet).</summary>
    private async Task<BorrowItem?> EnsureCanReportAsync(ComponentItem item, Guid userId, CancellationToken ct)
    {
        EnsureSupportedItem(item);
        EnsureInventoryManagerCreateStatus(item);
        var activeBorrowItem = await ResolveActiveBorrowItemAsync(item, ct);
        await EnsureCanCreateAsync(item, userId, ct);

        if (await HasOpenReportAsync(item.ItemId, ct))
            throw new ConflictException("Linh kiện này đã có báo cáo sự cố đang chờ xử lý");

        return activeBorrowItem;
    }

    /// <summary>Admin, or lab staff of the unit's lab, with ISSUE_RESOLVE.</summary>
    private async Task<bool> CanConfirmAsync(ComponentItem item, Guid userId, CancellationToken ct)
    {
        if (!_currentUser.HasPermission(PermissionCodes.IssueResolve))
            return false;
        if (_currentUser.IsInRole(RoleNames.Admin))
            return true;

        return _currentUser.IsInRole(RoleNames.LabStaff) &&
               await IsAssignedToRoomAsync(item.Cabinet.RoomId, userId, ct);
    }

    private async Task<StoredFile?> UploadImageAsync(CreateIssueReportRequest request, CancellationToken ct)
    {
        if (request.Image is null)
            return null;

        await using var imageStream = request.Image.OpenReadStream();
        var image = await _fileStorage.UploadImageAsync(imageStream, request.Image.FileName, IssueImageFolder, ct);
        if (image.Url.Length > 500)
        {
            await TryDeleteUploadedImageAsync(image);
            throw new BadRequestException("URL ảnh minh chứng vượt quá giới hạn lưu trữ");
        }

        return image;
    }

    /// <summary>Deletes the image of a failed create, unless a report was saved with it before the failure.</summary>
    private async Task DeleteUnusedImageAsync(StoredFile image)
    {
        var used = await _uow.Repository<IssueReport>().QueryNoTracking()
            .AnyAsync(report => report.ImageUrl == image.Url);
        if (!used)
            await TryDeleteUploadedImageAsync(image);
    }

    public async Task<IssueReportDetailResponse> ResolveAsync(
        Guid issueReportId,
        ResolveIssueReportRequest request,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = IssueReportStatuses.Normalize(request.Result);
        if (result is not (IssueReportStatuses.Confirmed or IssueReportStatuses.Rejected))
            throw new BadRequestException("Kết quả xử lý phải là Confirmed hoặc Rejected");

        var report = await FindReportAsync(issueReportId, ct);
        await EnsureCanResolveAsync(report, userId, ct);
        EnsureOpenReport(report);
        EnsureCanResolveDirectly(report, result);

        await using var transaction = await _uow.BeginTransactionAsync(ct);
        try
        {
            report = await FindReportAsync(issueReportId, ct);
            await EnsureCanResolveAsync(report, userId, ct);
            EnsureOpenReport(report);
            EnsureCanResolveDirectly(report, result);
            EnsureConsistentItemState(report);

            var now = DateTime.UtcNow;
            var affectedReports = await _uow.Repository<IssueReport>().Query()
                .Where(candidate => candidate.IssueReportId == issueReportId &&
                                    candidate.Status == IssueReportStatuses.Open)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.Status, result)
                    .SetProperty(candidate => candidate.ResolvedBy, userId)
                    .SetProperty(candidate => candidate.ResolvedAt, now), ct);

            if (affectedReports != 1)
                throw new ConflictException("Báo cáo sự cố đã được xử lý bởi yêu cầu khác");

            if (result == IssueReportStatuses.Confirmed)
            {
                await IssueReportConfirmationHandler.ConfirmAsync(
                    _uow,
                    report,
                    userId,
                    request.Note,
                    now,
                    ct);
            }

            await _uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await LoadDetailAsync(issueReportId, ct);
    }

    public async Task<IssueReportDetailResponse> CancelAsync(
        Guid issueReportId,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var report = await FindReportAsync(issueReportId, ct);
        EnsureCanCancel(report, userId);
        EnsureOpenReport(report);

        await using var transaction = await _uow.BeginTransactionAsync(ct);
        try
        {
            report = await FindReportAsync(issueReportId, ct);
            EnsureCanCancel(report, userId);
            EnsureOpenReport(report);
            EnsureConsistentItemState(report);

            var now = DateTime.UtcNow;
            var affectedReports = await _uow.Repository<IssueReport>().Query()
                .Where(candidate => candidate.IssueReportId == issueReportId &&
                                    candidate.Status == IssueReportStatuses.Open)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.Status, IssueReportStatuses.Cancelled)
                    .SetProperty(candidate => candidate.ResolvedBy, userId)
                    .SetProperty(candidate => candidate.ResolvedAt, now), ct);

            if (affectedReports != 1)
                throw new ConflictException("Báo cáo sự cố đã được xử lý bởi yêu cầu khác");

            await _uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await LoadDetailAsync(issueReportId, ct);
    }

    public async Task<PagedResult<IssueReportListItemResponse>> GetAsync(
        IssueReportQueryParams query,
        PageParams paging,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var reports = ApplyReadScope(ListQuery(), userId);
        if (query.Mine == true)
            reports = reports.Where(report => report.ReportedBy == userId);

        reports = ApplyFilters(reports, query);

        var pageNumber = Math.Max(1, paging.Page);
        var pageSize = Math.Clamp(paging.Size, 1, 100);
        var totalCount = await reports.CountAsync(ct);
        var entities = await reports
            .OrderByDescending(report => report.CreatedAt)
            .ThenByDescending(report => report.IssueReportId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<IssueReportListItemResponse>
        {
            Items = entities.Select(IssueReportMappings.ToListItemResponse).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<IssueReportDetailResponse> GetByIdAsync(
        Guid issueReportId,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var exists = await _uow.Repository<IssueReport>().QueryNoTracking()
            .AnyAsync(report => report.IssueReportId == issueReportId, ct);
        if (!exists)
            throw new NotFoundException("Không tìm thấy báo cáo sự cố");

        var report = await ApplyReadScope(DetailQuery(), userId)
            .FirstOrDefaultAsync(candidate => candidate.IssueReportId == issueReportId, ct);
        if (report is null)
            throw new ForbiddenException("Bạn không được xem báo cáo sự cố ngoài phạm vi của mình");

        return IssueReportMappings.ToDetailResponse(report);
    }

    private Guid GetCurrentUserId()
        => _currentUser.UserId ?? throw new UnauthorizedException();

    private async Task<ComponentItem> FindItemAsync(
        Guid? componentItemId,
        string? itemCode,
        CancellationToken ct)
    {
        var normalizedCode = itemCode?.Trim();
        if (!componentItemId.HasValue && string.IsNullOrWhiteSpace(normalizedCode))
            throw new BadRequestException("Phải cung cấp ComponentItemId hoặc ItemCode");
        if (componentItemId == Guid.Empty)
            throw new BadRequestException("ComponentItemId không hợp lệ");

        ComponentItem? item;
        if (componentItemId.HasValue)
        {
            item = await ItemQuery()
                .FirstOrDefaultAsync(candidate => candidate.ItemId == componentItemId.Value, ct);

            if (item is not null && !string.IsNullOrWhiteSpace(normalizedCode) &&
                !string.Equals(item.ItemCode, normalizedCode, StringComparison.Ordinal))
                throw new BadRequestException("ComponentItemId và ItemCode không cùng xác định một linh kiện");
        }
        else
        {
            item = await ItemQuery()
                .FirstOrDefaultAsync(candidate => candidate.ItemCode == normalizedCode, ct);
        }

        return item ?? throw new NotFoundException("Không tìm thấy linh kiện theo mã đã cung cấp");
    }

    private async Task<ComponentItem> FindItemByIdAsync(Guid itemId, CancellationToken ct)
        => await ItemQuery().FirstOrDefaultAsync(item => item.ItemId == itemId, ct)
           ?? throw new NotFoundException("Không tìm thấy linh kiện");

    private IQueryable<ComponentItem> ItemQuery()
        => _uow.Repository<ComponentItem>().QueryNoTracking()
            .Include(item => item.Component)
            .Include(item => item.Cabinet)
                .ThenInclude(cabinet => cabinet.Room);

    private static void EnsureSupportedItem(ComponentItem item)
    {
        if (!item.Component.TrackingType.Equals(TrackingReusable, StringComparison.OrdinalIgnoreCase) &&
            !item.Component.TrackingType.Equals(TrackingHighValue, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ thiết bị REUSABLE hoặc HIGH_VALUE mới được báo sự cố");

        if (item.Status is not (ComponentItemAvailable or ComponentItemBorrowed))
            throw new BadRequestException($"Thiết bị đang ở trạng thái {item.Status}, không ghi nhận sự cố được");
    }

    private async Task<BorrowItem?> ResolveActiveBorrowItemAsync(ComponentItem item, CancellationToken ct)
    {
        if (item.Status != ComponentItemBorrowed)
            return null;

        var matches = await _uow.Repository<BorrowItem>().QueryNoTracking()
            .Include(borrowItem => borrowItem.BorrowRequest)
                .ThenInclude(request => request.Requester)
                    .ThenInclude(requester => requester.StudentProfile)
            .Include(borrowItem => borrowItem.BorrowRequest)
                .ThenInclude(request => request.Class)
            .Include(borrowItem => borrowItem.BorrowRequest)
                .ThenInclude(request => request.Room)
            .Where(borrowItem => borrowItem.ComponentItemId == item.ItemId &&
                                 borrowItem.Status == BorrowItemIssued &&
                                 borrowItem.IssuedAt != null &&
                                 borrowItem.ReturnedAt == null &&
                                 (borrowItem.BorrowRequest.Status == BorrowRequestBorrowing ||
                                  borrowItem.BorrowRequest.Status == BorrowRequestOverdue) &&
                                 borrowItem.BorrowRequest.ReturnedAt == null)
            .Take(2)
            .ToListAsync(ct);

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ConflictException(
                "Linh kiện đang Borrowed nhưng không tìm thấy dòng mượn đang hoạt động"),
            _ => throw new ConflictException(
                "Linh kiện đang có nhiều dòng mượn hoạt động, không thể xác định báo cáo sự cố"),
        };
    }

    private void EnsureInventoryManagerCreateStatus(ComponentItem item)
    {
        if (_currentUser.IsInRole(RoleNames.Admin) ||
            !_currentUser.IsInRole(RoleNames.InventoryManager))
            return;

        if (item.Status == ComponentItemBorrowed)
            throw new ForbiddenException(
                "InventoryManager không được báo sự cố cho linh kiện đang được mượn");
    }

    private async Task EnsureCanCreateAsync(
        ComponentItem item,
        Guid userId,
        CancellationToken ct)
    {
        if (_currentUser.IsInRole(RoleNames.Admin))
            return;

        if (_currentUser.IsInRole(RoleNames.InventoryManager))
        {
            if (item.Status == ComponentItemAvailable &&
                (item.Cabinet is null || item.Cabinet.Room is null))
                throw new ConflictException(
                    "Linh kiện Available không có vị trí tủ và phòng lab hợp lệ trong kho");

            return;
        }

        if (_currentUser.IsInRole(RoleNames.LabStaff) &&
            await IsAssignedToRoomAsync(item.Cabinet.RoomId, userId, ct))
            return;

        throw new ForbiddenException(
            "Bạn chỉ ghi nhận được sự cố của thiết bị thuộc lab mình phụ trách");
    }

    private Task<bool> IsAssignedToRoomAsync(Guid roomId, Guid userId, CancellationToken ct)
        => _uow.Repository<LabRoomStaff>().QueryNoTracking()
            .AnyAsync(assignment => assignment.RoomId == roomId && assignment.UserId == userId, ct);

    private Task<bool> HasOpenReportAsync(Guid itemId, CancellationToken ct)
        => _uow.Repository<IssueReport>().QueryNoTracking()
            .AnyAsync(report => report.ComponentItemId == itemId &&
                                report.Status == IssueReportStatuses.Open, ct);

    private async Task<IssueReport> FindReportAsync(Guid issueReportId, CancellationToken ct)
        => await DetailQuery()
               .FirstOrDefaultAsync(report => report.IssueReportId == issueReportId, ct)
           ?? throw new NotFoundException("Không tìm thấy báo cáo sự cố");

    private async Task EnsureCanResolveAsync(IssueReport report, Guid userId, CancellationToken ct)
    {
        if (!_currentUser.HasPermission(PermissionCodes.IssueResolve))
            throw new ForbiddenException("Bạn không có quyền ISSUE_RESOLVE");

        if (_currentUser.IsInRole(RoleNames.Admin))
            return;

        if (_currentUser.IsInRole(RoleNames.LabStaff) &&
            await IsAssignedToRoomAsync(report.ComponentItem.Cabinet.RoomId, userId, ct))
            return;

        throw new ForbiddenException("Chỉ LabStaff phụ trách đúng lab hoặc Admin mới được xử lý sự cố");
    }

    private void EnsureCanCancel(IssueReport report, Guid userId)
    {
        if (report.ReportedBy == userId || _currentUser.IsInRole(RoleNames.Admin))
            return;

        throw new ForbiddenException("Chỉ người tạo báo cáo hoặc Admin mới được hủy báo cáo");
    }

    private static void EnsureOpenReport(IssueReport report)
    {
        if (report.Status != IssueReportStatuses.Open)
            throw new ConflictException("Chỉ báo cáo đang ở trạng thái Open mới được xử lý");
    }

    private static void EnsureCanResolveDirectly(IssueReport report, string result)
    {
        if (result == IssueReportStatuses.Confirmed && report.BorrowItemId.HasValue)
            throw new ConflictException(
                "Báo cáo gắn dòng mượn phải được xác nhận trong quy trình nhận trả của Borrowing");
    }

    private static void EnsureConsistentItemState(IssueReport report)
    {
        if (report.BorrowItemId is null)
        {
            if (report.ComponentItem.Status != ComponentItemAvailable)
                throw new ConflictException(
                    "Báo cáo tại tủ yêu cầu linh kiện còn Available nhưng trạng thái hiện tại đã thay đổi");

            return;
        }

        var borrowItem = report.BorrowItem;
        if (borrowItem is null ||
            borrowItem.BorrowItemId != report.BorrowItemId.Value ||
            borrowItem.ComponentItemId != report.ComponentItemId ||
            borrowItem.ComponentId != report.ComponentItem.ComponentId ||
            borrowItem.Quantity != 1 || borrowItem.IssuedAt is null)
            throw new ConflictException("Báo cáo không liên kết đúng dòng mượn của linh kiện");

        // Historical Good returns can close a report without touching the item's next loan.
        if (borrowItem.Status == BorrowItemReturned &&
            borrowItem.ReturnCondition == ReturnConditionGood &&
            borrowItem.ReturnedAt is not null && borrowItem.ReturnedQuantity == 1)
            return;

        if (report.ComponentItem.Status != ComponentItemBorrowed ||
            borrowItem.Status != BorrowItemIssued || borrowItem.ReturnedAt is not null ||
            borrowItem.BorrowRequest.Status is not (BorrowRequestBorrowing or BorrowRequestOverdue) ||
            borrowItem.BorrowRequest.ReturnedAt is not null)
            throw new ConflictException("Dòng mượn liên kết không còn ở trạng thái hoạt động hoặc đã trả Good hợp lệ");
    }

    private async Task<IssueReportDetailResponse> LoadDetailAsync(
        Guid issueReportId,
        CancellationToken ct)
    {
        var report = await DetailQuery()
            .FirstOrDefaultAsync(candidate => candidate.IssueReportId == issueReportId, ct)
            ?? throw new NotFoundException("Không tìm thấy báo cáo sự cố");

        return IssueReportMappings.ToDetailResponse(report);
    }

    private IQueryable<IssueReport> DetailQuery()
        => _uow.Repository<IssueReport>().QueryNoTracking()
            .Include(report => report.ReportedByNavigation)
            .Include(report => report.ResolvedByNavigation)
            .Include(report => report.ComponentItem)
                .ThenInclude(item => item.Component)
            .Include(report => report.ComponentItem)
                .ThenInclude(item => item.Cabinet)
                    .ThenInclude(cabinet => cabinet.Room)
            .Include(report => report.BorrowItem)
                .ThenInclude(borrowItem => borrowItem!.BorrowRequest)
                    .ThenInclude(request => request.Requester)
                        .ThenInclude(requester => requester.StudentProfile)
            .Include(report => report.BorrowItem)
                .ThenInclude(borrowItem => borrowItem!.BorrowRequest)
                    .ThenInclude(request => request.Class)
            .Include(report => report.BorrowItem)
                .ThenInclude(borrowItem => borrowItem!.BorrowRequest)
                    .ThenInclude(request => request.Room)
            .AsSplitQuery();

    private IQueryable<IssueReport> ListQuery()
        => _uow.Repository<IssueReport>().QueryNoTracking()
            .Include(report => report.ReportedByNavigation)
            .Include(report => report.ComponentItem)
                .ThenInclude(item => item.Component)
            .Include(report => report.ComponentItem)
                .ThenInclude(item => item.Cabinet)
                    .ThenInclude(cabinet => cabinet.Room)
            .AsSplitQuery();

    private IQueryable<IssueReport> ApplyReadScope(IQueryable<IssueReport> query, Guid userId)
    {
        if (_currentUser.IsInRole(RoleNames.Admin))
            return query;

        if (_currentUser.IsInRole(RoleNames.InventoryManager))
            return query;

        var isStudent = _currentUser.IsInRole(RoleNames.Student);
        var isLabStaff = _currentUser.IsInRole(RoleNames.LabStaff);
        var isLecturer = _currentUser.IsInRole(RoleNames.Lecturer);
        if (!isStudent && !isLabStaff && !isLecturer)
            throw new ForbiddenException("Vai trò hiện tại không được xem báo cáo sự cố");

        return query.Where(report =>
            (isStudent &&
             report.BorrowItem != null &&
             (report.BorrowItem.BorrowRequest.RequesterId == userId ||
              (report.BorrowItem.BorrowRequest.Project != null &&
               report.BorrowItem.BorrowRequest.Project.Team != null &&
               (report.BorrowItem.BorrowRequest.Project.Team.Leader.UserId == userId ||
                report.BorrowItem.BorrowRequest.Project.Team.TeamMembers.Any(member =>
                    member.Student.UserId == userId && member.Status == TeamMemberStatuses.Accepted))))) ||
            (isLabStaff && report.ComponentItem.Cabinet.Room.LabRoomStaffs
                .Any(assignment => assignment.UserId == userId)) ||
            (isLecturer &&
             report.BorrowItem != null &&
             report.BorrowItem.BorrowRequest.Class != null &&
             report.BorrowItem.BorrowRequest.Class.Instructor != null &&
             report.BorrowItem.BorrowRequest.Class.Instructor.UserId == userId));
    }

    private static IQueryable<IssueReport> ApplyFilters(
        IQueryable<IssueReport> reports,
        IssueReportQueryParams query)
    {
        if (query.ProjectId.HasValue)
            reports = reports.Where(report =>
                report.BorrowItem != null && report.BorrowItem.BorrowRequest.ProjectId == query.ProjectId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = IssueReportStatuses.Normalize(query.Status)
                         ?? throw new BadRequestException("Trạng thái báo cáo sự cố không hợp lệ");
            reports = reports.Where(report => report.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.IssueType))
        {
            var issueType = IssueTypes.Normalize(query.IssueType)
                            ?? throw new BadRequestException("Loại sự cố không hợp lệ");
            reports = reports.Where(report => report.IssueType == issueType);
        }

        if (query.RoomId.HasValue)
            reports = reports.Where(report =>
                report.ComponentItem.Cabinet.RoomId == query.RoomId.Value);

        if (query.ComponentId.HasValue)
            reports = reports.Where(report =>
                report.ComponentItem.ComponentId == query.ComponentId.Value);

        if (query.ComponentItemId.HasValue)
            reports = reports.Where(report => report.ComponentItemId == query.ComponentItemId.Value);

        if (!string.IsNullOrWhiteSpace(query.ItemCode))
        {
            var itemCode = query.ItemCode.Trim();
            reports = reports.Where(report => report.ComponentItem.ItemCode == itemCode);
        }

        if (query.ReportedBy.HasValue)
            reports = reports.Where(report => report.ReportedBy == query.ReportedBy.Value);

        if (query.FromDate.HasValue)
            reports = reports.Where(report => report.CreatedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            reports = reports.Where(report => report.CreatedAt <= query.ToDate.Value);

        return reports;
    }

    private async Task TryDeleteUploadedImageAsync(StoredFile uploadedImage)
    {
        try
        {
            await _fileStorage.DeleteAsync(uploadedImage.Key, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not clean up issue-report image {StorageKey}", uploadedImage.Key);
        }
    }
}

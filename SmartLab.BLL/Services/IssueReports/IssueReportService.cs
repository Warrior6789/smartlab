using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.IssueReports;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.External.Storage;
using SmartLab.BLL.Interfaces.IssueReports;
using SmartLab.BLL.Mappings.IssueReports;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.IssueReports;

public class IssueReportService : IIssueReportService
{
    private const string ComponentItemAvailable = "Available";
    private const string ComponentItemBorrowed = "Borrowed";
    private const string ComponentItemBroken = "Broken";
    private const string BorrowItemIssued = "Issued";
    private const string BorrowRequestBorrowing = "Borrowing";
    private const string BorrowRequestOverdue = "Overdue";
    private const string TrackingReusable = "REUSABLE";
    private const string TrackingHighValue = "HIGH_VALUE";
    private const string InventoryMove = "Move";
    private const string IssueImageFolder = "issues";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<IssueReportService> _logger;

    public IssueReportService(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IFileStorage fileStorage,
        ILogger<IssueReportService> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _logger = logger;
    }

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
        EnsureInventoryManagerCreateStatus(item);
        var activeBorrowItem = await ResolveActiveBorrowItemAsync(item, ct);
        await EnsureCanCreateAsync(item, activeBorrowItem, userId, ct);

        if (await HasOpenReportAsync(item.ItemId, ct))
            throw new ConflictException("Linh kiện này đã có báo cáo sự cố đang chờ xử lý");
        EnsureSupportedItem(item);

        StoredFile? uploadedImage = null;
        var databaseCommitted = false;

        try
        {
            if (request.Image is not null)
            {
                await using var imageStream = request.Image.OpenReadStream();
                uploadedImage = await _fileStorage.UploadImageAsync(
                    imageStream,
                    request.Image.FileName,
                    IssueImageFolder,
                    ct);

                if (uploadedImage.Url.Length > 500)
                    throw new BadRequestException("URL ảnh minh chứng vượt quá giới hạn lưu trữ");
            }

            var reportId = Guid.NewGuid();
            await using var transaction = await _uow.BeginTransactionAsync(ct);

            try
            {
                item = await FindItemByIdAsync(item.ItemId, ct);
                EnsureInventoryManagerCreateStatus(item);
                activeBorrowItem = await ResolveActiveBorrowItemAsync(item, ct);
                await EnsureCanCreateAsync(item, activeBorrowItem, userId, ct);

                if (await HasOpenReportAsync(item.ItemId, ct))
                    throw new ConflictException("Linh kiện này đã có báo cáo sự cố đang chờ xử lý");
                EnsureSupportedItem(item);

                var now = DateTime.UtcNow;
                if (item.Status == ComponentItemAvailable)
                {
                    var affectedItems = await _uow.Repository<ComponentItem>().Query()
                        .Where(candidate => candidate.ItemId == item.ItemId &&
                                            candidate.Status == ComponentItemAvailable)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(candidate => candidate.Status, ComponentItemBroken)
                            .SetProperty(candidate => candidate.UpdatedAt, now), ct);

                    if (affectedItems != 1)
                        throw new ConflictException("Trạng thái linh kiện đã thay đổi, vui lòng tải lại dữ liệu");

                    await _uow.Repository<InventoryTransaction>().AddAsync(
                        CreateInventoryTransaction(
                            item,
                            userId,
                            InventoryMove,
                            0,
                            $"Available -> Broken: sự cố {reportId}",
                            now),
                        ct);
                }

                await _uow.Repository<IssueReport>().AddAsync(new IssueReport
                {
                    IssueReportId = reportId,
                    BorrowItemId = activeBorrowItem?.BorrowItemId,
                    ComponentItemId = item.ItemId,
                    ReportedBy = userId,
                    IssueType = issueType,
                    Description = description,
                    ImageUrl = uploadedImage?.Url,
                    Status = IssueReportStatuses.Open,
                    CreatedAt = now,
                }, ct);

                await _uow.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                databaseCommitted = true;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            return await LoadDetailAsync(reportId, ct);
        }
        catch
        {
            if (uploadedImage is not null && !databaseCommitted)
                await TryDeleteUploadedImageAsync(uploadedImage);

            throw;
        }
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

        await using var transaction = await _uow.BeginTransactionAsync(ct);
        try
        {
            report = await FindReportAsync(issueReportId, ct);
            await EnsureCanResolveAsync(report, userId, ct);
            EnsureOpenReport(report);
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
            else if (report.BorrowItemId is null)
            {
                await RestoreBrokenItemAsync(report, userId, request.Note, now, ct);
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

            if (report.BorrowItemId is null)
                await RestoreBrokenItemAsync(report, userId, note: null, now, ct);

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
            throw new BadRequestException("Linh kiện phải ở trạng thái Available hoặc Borrowed để báo sự cố");
    }

    private async Task<BorrowItem?> ResolveActiveBorrowItemAsync(ComponentItem item, CancellationToken ct)
    {
        if (item.Status != ComponentItemBorrowed)
            return null;

        var matches = await _uow.Repository<BorrowItem>().QueryNoTracking()
            .Include(borrowItem => borrowItem.BorrowRequest)
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
        BorrowItem? activeBorrowItem,
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

        if (_currentUser.IsInRole(RoleNames.Student) &&
            activeBorrowItem?.BorrowRequest.RequesterId == userId)
            return;

        throw new ForbiddenException(
            "Bạn không được báo sự cố cho linh kiện ngoài phạm vi phụ trách hoặc mượn của mình");
    }

    private Task<bool> IsAssignedToRoomAsync(Guid roomId, Guid userId, CancellationToken ct)
        => _uow.Repository<LabRoomStaff>().QueryNoTracking()
            .AnyAsync(assignment => assignment.RoomId == roomId && assignment.UserId == userId, ct);

    private Task<bool> HasOpenReportAsync(Guid itemId, CancellationToken ct)
        => _uow.Repository<IssueReport>().QueryNoTracking()
            .AnyAsync(report => report.ComponentItemId == itemId &&
                                report.Status == IssueReportStatuses.Open, ct);

    private static InventoryTransaction CreateInventoryTransaction(
        ComponentItem item,
        Guid actorId,
        string type,
        int quantityChange,
        string reason,
        DateTime createdAt,
        Guid? borrowRequestId = null)
    {
        if (reason.Length > 255)
            throw new BadRequestException("Nội dung ghi chú làm lý do lịch sử kho vượt quá 255 ký tự");

        return new InventoryTransaction
        {
            TransactionId = Guid.NewGuid(),
            Type = type,
            ComponentId = item.ComponentId,
            CabinetId = item.CabinetId,
            ItemId = item.ItemId,
            QuantityChange = quantityChange,
            Reason = reason,
            CreatedBy = actorId,
            CreatedAt = createdAt,
            BorrowRequestId = borrowRequestId,
        };
    }

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

    private static void EnsureConsistentItemState(IssueReport report)
    {
        if (report.BorrowItemId is null && report.ComponentItem.Status != ComponentItemBroken)
            throw new ConflictException(
                "Báo cáo trong kho yêu cầu linh kiện ở trạng thái Broken nhưng dữ liệu hiện tại đã thay đổi");

        if (report.BorrowItemId.HasValue)
        {
            if (report.ComponentItem.Status != ComponentItemBorrowed)
                throw new ConflictException(
                    "Báo cáo khi mượn yêu cầu linh kiện ở trạng thái Borrowed nhưng dữ liệu hiện tại đã thay đổi");

            if (report.BorrowItem is null ||
                report.BorrowItem.BorrowItemId != report.BorrowItemId.Value ||
                report.BorrowItem.ComponentItemId != report.ComponentItemId ||
                report.BorrowItem.Status != BorrowItemIssued ||
                report.BorrowItem.IssuedAt is null ||
                report.BorrowItem.ReturnedAt is not null ||
                report.BorrowItem.BorrowRequest.Status is not
                    (BorrowRequestBorrowing or BorrowRequestOverdue) ||
                report.BorrowItem.BorrowRequest.ReturnedAt is not null)
                throw new ConflictException("Dòng mượn liên kết không còn ở trạng thái hoạt động");
        }
    }

    private async Task RestoreBrokenItemAsync(
        IssueReport report,
        Guid actorId,
        string? note,
        DateTime now,
        CancellationToken ct)
    {
        var affectedItems = await _uow.Repository<ComponentItem>().Query()
            .Where(item => item.ItemId == report.ComponentItemId &&
                           item.Status == ComponentItemBroken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, ComponentItemAvailable)
                .SetProperty(item => item.UpdatedAt, now), ct);

        if (affectedItems != 1)
            throw new ConflictException("Trạng thái linh kiện đã thay đổi, không thể hoàn tác báo cáo sự cố");

        var reason = AppendNote($"Broken -> Available: sự cố {report.IssueReportId}", note);
        await _uow.Repository<InventoryTransaction>().AddAsync(
            CreateInventoryTransaction(
                report.ComponentItem,
                actorId,
                InventoryMove,
                0,
                reason,
                now),
            ct);
    }

    private static string AppendNote(string prefix, string? note)
    {
        var normalizedNote = note?.Trim();
        var reason = string.IsNullOrWhiteSpace(normalizedNote)
            ? prefix
            : $"{prefix}, {normalizedNote}";

        if (reason.Length > 255)
            throw new BadRequestException(
                "Ghi chú quá dài để lưu trong lý do lịch sử kho (tối đa 255 ký tự cho toàn bộ nội dung)");

        return reason;
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
            .Include(report => report.BorrowItem)
                .ThenInclude(borrowItem => borrowItem!.BorrowRequest)
                    .ThenInclude(request => request.Class)
            .Include(report => report.BorrowItem)
                .ThenInclude(borrowItem => borrowItem!.SourceRoom)
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
            (isStudent && report.ReportedBy == userId) ||
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

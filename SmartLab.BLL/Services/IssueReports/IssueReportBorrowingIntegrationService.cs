using Microsoft.EntityFrameworkCore;

using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.IssueReports;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.IssueReports;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.IssueReports;

public class IssueReportBorrowingIntegrationService : IIssueReportBorrowingIntegration
{
    private const string ComponentItemBorrowed = "Borrowed";
    private const string BorrowItemIssued = "Issued";
    private const string BorrowRequestBorrowing = "Borrowing";
    private const string BorrowRequestOverdue = "Overdue";
    private const string TrackingReusable = "REUSABLE";
    private const string TrackingHighValue = "HIGH_VALUE";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public IssueReportBorrowingIntegrationService(
        IUnitOfWork uow,
        ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<ConfirmBorrowingReturnIssueResult> ConfirmReturnIssueAsync(
        ConfirmBorrowingReturnIssueCommand command,
        CancellationToken ct = default)
    {
        var actorId = GetAuthorizedActorId();
        var issueType = IssueTypes.Normalize(command.IssueType)
                        ?? throw new BadRequestException("Loại sự cố phải là Damaged, Burnt hoặc Lost");
        var description = command.Description?.Trim();

        if (command.BorrowItemId == Guid.Empty)
            throw new BadRequestException("BorrowItemId không hợp lệ");
        if (string.IsNullOrWhiteSpace(description))
            throw new BadRequestException("Mô tả sự cố là bắt buộc");
        if (description.Length > 1000)
            throw new BadRequestException("Mô tả sự cố không được vượt quá 1000 ký tự");

        var borrowItem = await FindBorrowItemAsync(command.BorrowItemId, ct);
        await EnsureCanConfirmReturnIssueAsync(borrowItem, actorId, ct);

        // Use the same item lock as Create, inside the Borrowing caller's transaction.
        var componentItemId = borrowItem.ComponentItemId!.Value;
        var affectedItems = await _uow.Repository<ComponentItem>().Query()
            .Where(item => item.ItemId == componentItemId &&
                           item.Status == ComponentItemBorrowed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, item => item.Status), ct);

        if (affectedItems != 1)
            throw new ConflictException("Trạng thái linh kiện đã thay đổi, không thể xác nhận sự cố");

        borrowItem = await FindBorrowItemAsync(command.BorrowItemId, ct);
        if (borrowItem.ComponentItemId != componentItemId)
            throw new ConflictException("Linh kiện của dòng mượn đã thay đổi, vui lòng tải lại dữ liệu");
        await EnsureCanConfirmReturnIssueAsync(borrowItem, actorId, ct);

        var openReports = await _uow.Repository<IssueReport>().QueryNoTracking()
            .Where(report => report.ComponentItemId == borrowItem.ComponentItemId &&
                             report.Status == IssueReportStatuses.Open)
            .OrderBy(report => report.CreatedAt)
            .ThenBy(report => report.IssueReportId)
            .Take(2)
            .ToListAsync(ct);

        if (openReports.Count > 1)
            throw new ConflictException(
                "Linh kiện đang có nhiều báo cáo Open, không thể tự động xác nhận khi nhận trả");

        var now = DateTime.UtcNow;
        var report = openReports.SingleOrDefault();
        var reusedOpenReport = report is not null;
        var finalIssueType = report?.IssueType == IssueTypes.Burnt && issueType == IssueTypes.Damaged
            ? IssueTypes.Burnt
            : issueType;

        if (report is not null)
        {
            if (report.BorrowItemId != borrowItem.BorrowItemId)
                throw new ConflictException(
                    "Báo cáo Open không liên kết đúng dòng mượn đang được nhận trả");

            var originalIssueType = report.IssueType;
            var affectedReports = await _uow.Repository<IssueReport>().Query()
                .Where(candidate => candidate.IssueReportId == report.IssueReportId &&
                                    candidate.Status == IssueReportStatuses.Open &&
                                    candidate.IssueType == originalIssueType &&
                                    candidate.BorrowItemId == borrowItem.BorrowItemId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.IssueType, finalIssueType)
                    .SetProperty(candidate => candidate.Status, IssueReportStatuses.Confirmed)
                    .SetProperty(candidate => candidate.ResolvedBy, actorId)
                    .SetProperty(candidate => candidate.ResolvedAt, now), ct);

            if (affectedReports != 1)
                throw new ConflictException("Báo cáo sự cố đã được xử lý bởi yêu cầu khác");
        }
        else
        {
            report = new IssueReport
            {
                IssueReportId = Guid.NewGuid(),
                BorrowItemId = borrowItem.BorrowItemId,
                ComponentItemId = borrowItem.ComponentItemId!.Value,
                ReportedBy = actorId,
                IssueType = finalIssueType,
                Description = description,
                Status = IssueReportStatuses.Confirmed,
                ResolvedBy = actorId,
                CreatedAt = now,
                ResolvedAt = now,
            };

            await _uow.Repository<IssueReport>().AddAsync(report, ct);
        }

        var confirmationReport = new IssueReport
        {
            IssueReportId = report.IssueReportId,
            BorrowItemId = borrowItem.BorrowItemId,
            ComponentItemId = borrowItem.ComponentItemId!.Value,
            IssueType = finalIssueType,
            ComponentItem = borrowItem.ComponentItem!,
            BorrowItem = borrowItem,
        };

        await IssueReportConfirmationHandler.ConfirmAsync(
            _uow,
            confirmationReport,
            actorId,
            note: null,
            now,
            ct);
        await _uow.SaveChangesAsync(ct);

        return new ConfirmBorrowingReturnIssueResult
        {
            IssueReportId = report.IssueReportId,
            ReusedOpenReport = reusedOpenReport,
            IssueType = finalIssueType,
            Status = IssueReportStatuses.Confirmed,
        };
    }

    private Guid GetAuthorizedActorId()
    {
        var actorId = _currentUser.UserId ?? throw new UnauthorizedException();

        if (!_currentUser.IsInRole(RoleNames.LabStaff) && !_currentUser.IsInRole(RoleNames.Admin))
            throw new ForbiddenException("Chỉ LabStaff hoặc Admin được xác nhận sự cố trong quy trình nhận trả");
        if (!_currentUser.HasPermission(PermissionCodes.IssueResolve))
            throw new ForbiddenException("Bạn không có quyền ISSUE_RESOLVE");

        return actorId;
    }

    private async Task<BorrowItem> FindBorrowItemAsync(Guid borrowItemId, CancellationToken ct)
        => await _uow.Repository<BorrowItem>().QueryNoTracking()
               .Include(item => item.BorrowRequest)
               .Include(item => item.SourceRoom)
               .Include(item => item.ComponentItem)
                   .ThenInclude(componentItem => componentItem!.Component)
               .Include(item => item.ComponentItem)
                   .ThenInclude(componentItem => componentItem!.Cabinet)
                       .ThenInclude(cabinet => cabinet.Room)
               .AsSplitQuery()
               .FirstOrDefaultAsync(item => item.BorrowItemId == borrowItemId, ct)
           ?? throw new NotFoundException("Không tìm thấy dòng mượn cần nhận trả");

    private async Task EnsureCanConfirmReturnIssueAsync(
        BorrowItem borrowItem,
        Guid actorId,
        CancellationToken ct)
    {
        if (!borrowItem.SourceRoomId.HasValue || borrowItem.SourceRoom is null)
            throw new ConflictException(
                "Dòng mượn không có SourceRoomId hợp lệ; không được fallback sang phòng của BorrowRequest");

        if (borrowItem.ComponentItemId is null || borrowItem.ComponentItem is null)
            throw new ConflictException("Dòng mượn không liên kết ComponentItem hợp lệ");

        if (borrowItem.ComponentId != borrowItem.ComponentItem.ComponentId)
            throw new ConflictException("Component của dòng mượn không khớp ComponentItem");

        if (borrowItem.BorrowRequest is null ||
            borrowItem.ComponentItem.Cabinet?.Room is null ||
            borrowItem.ComponentItem.Cabinet.RoomId != borrowItem.BorrowRequest.RoomId)
            throw new ConflictException(
                "Tủ của linh kiện không thuộc lab nhận đồ của đơn mượn");

        if (!_currentUser.IsInRole(RoleNames.Admin))
        {
            var assignedToPickupRoom = await _uow.Repository<LabRoomStaff>().QueryNoTracking()
                .AnyAsync(assignment => assignment.RoomId == borrowItem.BorrowRequest.RoomId &&
                                        assignment.UserId == actorId, ct);
            if (!assignedToPickupRoom)
                throw new ForbiddenException("LabStaff không được phân công tại lab nhận đồ của đơn mượn");
        }

        if (borrowItem.ComponentItem.Status != ComponentItemBorrowed ||
            borrowItem.Quantity != 1 ||
            borrowItem.Status != BorrowItemIssued ||
            borrowItem.IssuedAt is null ||
            borrowItem.ReturnedAt is not null ||
            borrowItem.BorrowRequest.Status is not
                (BorrowRequestBorrowing or BorrowRequestOverdue) ||
            borrowItem.BorrowRequest.ReturnedAt is not null)
            throw new ConflictException("Dòng mượn không còn ở trạng thái đang mượn hợp lệ");

        var activeBorrowItemCount = await _uow.Repository<BorrowItem>().QueryNoTracking()
            .CountAsync(item => item.ComponentItemId == borrowItem.ComponentItemId &&
                                item.Status == BorrowItemIssued && item.IssuedAt != null &&
                                item.ReturnedAt == null &&
                                (item.BorrowRequest.Status == BorrowRequestBorrowing ||
                                 item.BorrowRequest.Status == BorrowRequestOverdue) &&
                                item.BorrowRequest.ReturnedAt == null, ct);
        if (activeBorrowItemCount != 1)
            throw new ConflictException("Không thể xác định duy nhất dòng mượn đang hoạt động của linh kiện");

        var trackingType = borrowItem.ComponentItem.Component.TrackingType;
        if (!trackingType.Equals(TrackingReusable, StringComparison.OrdinalIgnoreCase) &&
            !trackingType.Equals(TrackingHighValue, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ thiết bị REUSABLE hoặc HIGH_VALUE mới được báo sự cố");
    }
}

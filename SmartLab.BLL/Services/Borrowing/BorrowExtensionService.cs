using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Extending the due date of a request being borrowed. Only the borrower (team leader who sent the request) asks, before
/// the due date has passed, at most system_settings BORROW_EXTENSION_MAX_DAYS days each time and
/// BORROW_EXTENSION_MAX_TIMES approved times per request, one pending at a time. The lecturer of the class (or an Admin)
/// approves or rejects. Every change first locks the request row, so asking and reviewing never run at the same time.
/// </summary>
public class BorrowExtensionService : IBorrowExtensionService
{
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);
    private static readonly string[] BorrowedStatuses = { BorrowRequestStatuses.Borrowing, BorrowRequestStatuses.Overdue };

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IBorrowNotifier _notifier;

    public BorrowExtensionService(IUnitOfWork uow, ICurrentUserService currentUser, IBorrowNotifier notifier)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<BorrowExtensionResponse> RequestAsync(
        Guid borrowRequestId, RequestExtensionRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(borrowRequestId, ct);

        var borrow = await _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .Select(r => new { r.RequesterId, r.Status, r.DueDate })
            .FirstAsync(ct);

        if (borrow.RequesterId != userId)
            throw new ForbiddenException("Chỉ nhóm trưởng (người gửi đơn mượn) được xin gia hạn");
        if (!BorrowedStatuses.Contains(borrow.Status))
            throw new ConflictException("Chỉ xin gia hạn được khi đơn đang mượn");
        if (borrow.DueDate < now.AddDays(-1))
            throw new BadRequestException("Đơn đã quá hạn trả nên không xin gia hạn được, hãy mang đồ đến trả");

        var extensions = _uow.Repository<BorrowExtension>();
        var hasPending = await extensions.QueryNoTracking()
            .AnyAsync(e => e.BorrowRequestId == borrowRequestId && e.Status == BorrowExtensionStatuses.Pending, ct);
        if (hasPending)
            throw new ConflictException("Đơn đang có một yêu cầu gia hạn chờ duyệt");

        var approvedCount = await extensions.QueryNoTracking()
            .CountAsync(e => e.BorrowRequestId == borrowRequestId && e.Status == BorrowExtensionStatuses.Approved, ct);
        var maxTimes = await SystemSettingValues.GetPositiveIntAsync(
            _uow, SystemSettingKeys.BorrowExtensionMaxTimes, "số lần gia hạn tối đa", ct);
        if (approvedCount >= maxTimes)
            throw new BadRequestException($"Mỗi đơn chỉ được gia hạn tối đa {maxTimes} lần");

        var newDueDate = ToVietnamDateUtc(request.NewDueDate!.Value);
        if (newDueDate <= borrow.DueDate)
            throw new BadRequestException("Ngày trả mới phải sau hạn trả hiện tại");
        var maxDays = await SystemSettingValues.GetPositiveIntAsync(
            _uow, SystemSettingKeys.BorrowExtensionMaxDays, "số ngày gia hạn tối đa", ct);
        if (newDueDate > borrow.DueDate.AddDays(maxDays))
            throw new BadRequestException($"Mỗi lần chỉ được gia hạn tối đa {maxDays} ngày");

        var extension = new BorrowExtension
        {
            BorrowRequestId = borrowRequestId,
            OldDueDate = borrow.DueDate,
            NewDueDate = newDueDate,
            Reason = request.Reason.Trim(),
            Status = BorrowExtensionStatuses.Pending,
            CreatedAt = now,
        };
        await extensions.AddAsync(extension, ct);
        await _uow.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        await _notifier.ExtensionRequestedAsync(extension.ExtensionId, ct);

        return await GetByIdAsync(extension.ExtensionId, ct);
    }

    public Task<PagedResult<BorrowExtensionResponse>> GetExtensionsAsync(
        BorrowExtensionQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var extensions = VisibleExtensions();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            extensions = extensions.Where(e => e.BorrowRequest.RequestCode.ToLower().Contains(keyword)
                                               || e.BorrowRequest.Requester.FullName.ToLower().Contains(keyword)
                                               || (e.BorrowRequest.Project != null
                                                   && e.BorrowRequest.Project.Title.ToLower().Contains(keyword)));
        }

        var status = BorrowExtensionStatuses.Normalize(query.Status);
        if (status != null)
            extensions = extensions.Where(e => e.Status == status);

        var result = ToResponses(extensions.OrderByDescending(e => e.CreatedAt).ThenBy(e => e.ExtensionId));

        return PagedResult<BorrowExtensionResponse>.CreateAsync(result, paging, ct);
    }

    /// <summary>The request's due date becomes the new one. The request must still be borrowed and its due date unchanged.</summary>
    public async Task<BorrowExtensionResponse> ApproveAsync(
        Guid extensionId, ApproveExtensionRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        var extension = await GetForReviewAsync(extensionId, ct);

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(extension.BorrowRequestId, ct);

        var borrow = await _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.BorrowRequestId == extension.BorrowRequestId)
            .Select(r => new { r.Status, r.DueDate })
            .FirstAsync(ct);
        if (!BorrowedStatuses.Contains(borrow.Status))
            throw new ConflictException("Đơn mượn không còn đang mượn, không duyệt gia hạn được");
        if (borrow.DueDate != extension.OldDueDate)
            throw new ConflictException("Hạn trả của đơn đã thay đổi, vui lòng từ chối để sinh viên xin lại");

        await DecideAsync(extensionId, BorrowExtensionStatuses.Approved, userId, CleanText(request.Comment), now, ct);

        await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.BorrowRequestId == extension.BorrowRequestId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.DueDate, extension.NewDueDate)
                .SetProperty(r => r.Status, BorrowRequestStatuses.Borrowing), ct);

        await tx.CommitAsync(ct);

        await _notifier.ExtensionReviewedAsync(extensionId, ct);

        return await GetByIdAsync(extensionId, ct);
    }

    public async Task<BorrowExtensionResponse> RejectAsync(
        Guid extensionId, RejectExtensionRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        var extension = await GetForReviewAsync(extensionId, ct);

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(extension.BorrowRequestId, ct);
        await DecideAsync(extensionId, BorrowExtensionStatuses.Rejected, userId, request.Reason.Trim(), now, ct);

        await tx.CommitAsync(ct);

        await _notifier.ExtensionReviewedAsync(extensionId, ct);

        return await GetByIdAsync(extensionId, ct);
    }

    private sealed record ReviewTarget(Guid BorrowRequestId, DateTime OldDueDate, DateTime NewDueDate);

    /// <summary>404 when missing, 403 unless Admin or the lecturer of the request's class, 409 when already decided.</summary>
    private async Task<ReviewTarget> GetForReviewAsync(Guid extensionId, CancellationToken ct)
    {
        var extension = await _uow.Repository<BorrowExtension>().QueryNoTracking()
                            .Where(e => e.ExtensionId == extensionId)
                            .Select(e => new
                            {
                                e.BorrowRequestId,
                                e.OldDueDate,
                                e.NewDueDate,
                                e.Status,
                                LecturerUserId = e.BorrowRequest.Class != null && e.BorrowRequest.Class.Instructor != null
                                    ? (Guid?)e.BorrowRequest.Class.Instructor.UserId
                                    : null,
                            })
                            .FirstOrDefaultAsync(ct)
                        ?? throw new NotFoundException("Không tìm thấy yêu cầu gia hạn");

        if (!_currentUser.IsInRole(RoleNames.Admin) && extension.LecturerUserId != _currentUser.UserId)
            throw new ForbiddenException("Chỉ giảng viên của lớp được duyệt yêu cầu gia hạn này");
        if (extension.Status != BorrowExtensionStatuses.Pending)
            throw new ConflictException("Yêu cầu gia hạn đã được xử lý");

        return new ReviewTarget(extension.BorrowRequestId, extension.OldDueDate, extension.NewDueDate);
    }

    /// <summary>Pending → <paramref name="status"/>; 409 when someone decided it meanwhile.</summary>
    private async Task DecideAsync(
        Guid extensionId, string status, Guid userId, string? comment, DateTime now, CancellationToken ct)
    {
        var decided = await _uow.Repository<BorrowExtension>().Query()
            .Where(e => e.ExtensionId == extensionId && e.Status == BorrowExtensionStatuses.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, status)
                .SetProperty(e => e.ReviewedBy, userId)
                .SetProperty(e => e.ReviewComment, comment)
                .SetProperty(e => e.ReviewedAt, now), ct);
        if (decided == 0)
            throw new ConflictException("Yêu cầu gia hạn đã được xử lý");
    }

    /// <summary>Locks the request row (no-op UPDATE), the same lock as the other borrowing actions; 404 when missing.</summary>
    private async Task LockRequestAsync(Guid borrowRequestId, CancellationToken ct)
    {
        var locked = await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, r => r.Status), ct);
        if (locked == 0)
            throw new NotFoundException("Không tìm thấy đơn mượn");
    }

    /// <summary>Admin sees all; a lecturer sees the extensions of their classes.</summary>
    private IQueryable<BorrowExtension> VisibleExtensions()
    {
        var extensions = _uow.Repository<BorrowExtension>().QueryNoTracking();
        if (_currentUser.IsInRole(RoleNames.Admin))
            return extensions;

        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        return extensions.Where(e => e.BorrowRequest.Class != null
                                     && e.BorrowRequest.Class.Instructor != null
                                     && e.BorrowRequest.Class.Instructor.UserId == userId);
    }

    private Task<BorrowExtensionResponse> GetByIdAsync(Guid extensionId, CancellationToken ct)
        => ToResponses(_uow.Repository<BorrowExtension>().QueryNoTracking().Where(e => e.ExtensionId == extensionId))
            .FirstAsync(ct);

    private static IQueryable<BorrowExtensionResponse> ToResponses(IQueryable<BorrowExtension> extensions)
        => extensions.Select(e => new BorrowExtensionResponse
        {
            ExtensionId = e.ExtensionId,
            BorrowRequestId = e.BorrowRequestId,
            RequestCode = e.BorrowRequest.RequestCode,
            RequesterName = e.BorrowRequest.Requester.FullName,
            ClassCode = e.BorrowRequest.Class != null ? e.BorrowRequest.Class.ClassCode : null,
            ProjectTitle = e.BorrowRequest.Project != null ? e.BorrowRequest.Project.Title : null,
            OldDueDate = e.OldDueDate,
            NewDueDate = e.NewDueDate,
            Reason = e.Reason,
            Status = e.Status,
            ReviewedBy = e.ReviewedBy,
            ReviewedByName = e.ReviewedByNavigation != null ? e.ReviewedByNavigation.FullName : null,
            ReviewComment = e.ReviewComment,
            CreatedAt = e.CreatedAt,
            ReviewedAt = e.ReviewedAt,
        });

    /// <summary>
    /// Start of the Vietnam calendar day, in UTC. A plain date ("2026-10-28") is that day. A value with "Z" or an offset
    /// is first turned into Vietnam time, so the server time zone never shifts the day.
    /// </summary>
    private static DateTime ToVietnamDateUtc(DateTime vietnamDate)
    {
        var date = vietnamDate.Date;
        if (vietnamDate.Kind != DateTimeKind.Unspecified)
            date = (vietnamDate.ToUniversalTime() + VietnamUtcOffset).Date;

        return DateTime.SpecifyKind(date - VietnamUtcOffset, DateTimeKind.Utc);
    }

    private static string? CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Trim();
    }
}

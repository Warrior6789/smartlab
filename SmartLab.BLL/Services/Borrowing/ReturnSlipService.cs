using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Pending → Approved (staff of the request's lab, who pick the day to come) → Completed (goods received by
/// BorrowRequestService.ReturnAsync);
/// or Rejected (staff, with a reason) / Cancelled (borrower). A borrowed line is on at most one open slip. Every change
/// first locks the request row, the same lock as handing over and receiving, so they never run at the same time.
/// </summary>
public class ReturnSlipService : IReturnSlipService
{
    private const string SlipCodeUniqueConstraint = "borrow_return_slips_slip_code_key";
    private const int MaxCodeAttempts = 3;

    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);
    private static readonly string[] BorrowedStatuses = { BorrowRequestStatuses.Borrowing, BorrowRequestStatuses.Overdue };

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IBorrowNotifier _notifier;
    private readonly IChangeBroadcaster _changes;

    public ReturnSlipService(IUnitOfWork uow, ICurrentUserService currentUser, IBorrowNotifier notifier, IChangeBroadcaster changes)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifier = notifier;
        _changes = changes;
    }

    public async Task<ReturnSlipDetailResponse> CreateAsync(
        Guid borrowRequestId, CreateReturnSlipRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        await EnsureBorrowerAsync(borrowRequestId, userId, ct);

        var slip = new BorrowReturnSlip
        {
            BorrowRequestId = borrowRequestId,
            Status = ReturnSlipStatuses.Pending,
            Note = CleanText(request.Note),
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _uow.Repository<BorrowReturnSlip>().AddAsync(slip, ct);

        var attempt = 1;
        while (true)
        {
            await using var tx = await _uow.BeginTransactionAsync(ct);

            await LockBorrowedRequestAsync(borrowRequestId, "Chỉ tạo phiếu trả cho đơn đang mượn", ct);
            var lines = await GetSlipLinesAsync(borrowRequestId, request, ct);

            slip.SlipCode = await GenerateSlipCodeAsync(now, ct);
            slip.BorrowReturnSlipItems.Clear();
            foreach (var line in lines)
                slip.BorrowReturnSlipItems.Add(line);

            try
            {
                await _uow.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (attempt < MaxCodeAttempts
                                               && ex.InnerException is PostgresException
                                               {
                                                   SqlState: PostgresErrorCodes.UniqueViolation,
                                                   ConstraintName: SlipCodeUniqueConstraint,
                                               })
            {
                attempt++;
            }
        }

        await _notifier.ReturnSlipCreatedAsync(slip.ReturnSlipId, ct);
        await _changes.BorrowRequestChangedAsync(borrowRequestId);

        return await GetByIdAsync(slip.ReturnSlipId, ct);
    }

    public Task<PagedResult<ReturnSlipResponse>> GetMySlipsAsync(
        ReturnSlipQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var slips = _uow.Repository<BorrowReturnSlip>().QueryNoTracking()
            .Where(s => s.BorrowRequest.RequesterId == userId
                        || (s.BorrowRequest.Project != null && s.BorrowRequest.Project.Team != null
                            && (s.BorrowRequest.Project.Team.Leader.UserId == userId
                                || s.BorrowRequest.Project.Team.TeamMembers.Any(tm =>
                                    tm.Student.UserId == userId && tm.Status == TeamMemberStatuses.Accepted))));

        return ToPagedResponses(Filter(slips, query), paging, ct);
    }

    public Task<PagedResult<ReturnSlipResponse>> GetSlipsAsync(
        ReturnSlipQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var slips = _uow.Repository<BorrowReturnSlip>().QueryNoTracking();

        if (!_currentUser.IsInRole(RoleNames.Admin))
        {
            var myRooms = MyRoomIds();
            slips = slips.Where(s => myRooms.Contains(s.BorrowRequest.RoomId));
        }

        return ToPagedResponses(Filter(slips, query), paging, ct);
    }

    public async Task<ReturnSlipDetailResponse> GetByIdAsync(Guid returnSlipId, CancellationToken ct = default)
    {
        if (!await _uow.Repository<BorrowReturnSlip>().QueryNoTracking().AnyAsync(s => s.ReturnSlipId == returnSlipId, ct))
            throw new NotFoundException("Không tìm thấy phiếu trả");

        var userId = _currentUser.UserId;
        var today = TodayUtc(DateTime.UtcNow);
        return await VisibleSlips()
                   .Where(s => s.ReturnSlipId == returnSlipId)
                   .Select(s => new ReturnSlipDetailResponse
                   {
                       ReturnSlipId = s.ReturnSlipId,
                       SlipCode = s.SlipCode,
                       Status = s.Status,
                       IsLate = s.Status == ReturnSlipStatuses.Approved && s.AppointmentDate < today,
                       AppointmentDate = s.AppointmentDate,
                       BorrowRequestId = s.BorrowRequestId,
                       RequestCode = s.BorrowRequest.RequestCode,
                       DueDate = s.BorrowRequest.DueDate,
                       RequesterId = s.BorrowRequest.RequesterId,
                       RequesterName = s.BorrowRequest.Requester.FullName,
                       StudentCode = s.BorrowRequest.Requester.StudentProfile != null
                           ? s.BorrowRequest.Requester.StudentProfile.StudentCode
                           : null,
                       ClassCode = s.BorrowRequest.Class != null ? s.BorrowRequest.Class.ClassCode : null,
                       ProjectTitle = s.BorrowRequest.Project != null ? s.BorrowRequest.Project.Title : null,
                       RoomId = s.BorrowRequest.RoomId,
                       RoomCode = s.BorrowRequest.Room.RoomCode,
                       RoomName = s.BorrowRequest.Room.RoomName,
                       ItemCount = s.BorrowReturnSlipItems.Count,
                       StillOutCount = s.BorrowRequest.BorrowItems.Count(i => i.Status == BorrowItemStatuses.Issued),
                       CreatedAt = s.CreatedAt,
                       Note = s.Note,
                       CreatedBy = s.CreatedBy,
                       CreatedByName = s.CreatedByNavigation.FullName,
                       ReviewedBy = s.ReviewedBy,
                       ReviewedByName = s.ReviewedByNavigation != null ? s.ReviewedByNavigation.FullName : null,
                       ReviewedAt = s.ReviewedAt,
                       ReviewComment = s.ReviewComment,
                       ReceivedBy = s.ReceivedBy,
                       ReceivedByName = s.ReceivedByNavigation != null ? s.ReceivedByNavigation.FullName : null,
                       ReceivedAt = s.ReceivedAt,
                       CanCancel = ReturnSlipStatuses.Open.Contains(s.Status)
                                   && (s.BorrowRequest.RequesterId == userId
                                       || (s.BorrowRequest.Project != null && s.BorrowRequest.Project.Team != null
                                           && s.BorrowRequest.Project.Team.Leader.UserId == userId)),
                       Items = s.BorrowReturnSlipItems
                           .OrderBy(i => i.BorrowItem.Component.ComponentCode).ThenBy(i => i.BorrowItemId)
                           .Select(i => new ReturnSlipItemResponse
                           {
                               BorrowItemId = i.BorrowItemId,
                               ComponentId = i.BorrowItem.ComponentId,
                               ComponentCode = i.BorrowItem.Component.ShortCode ?? i.BorrowItem.Component.ComponentCode,
                               ComponentName = i.BorrowItem.Component.ComponentName,
                               TrackingType = i.BorrowItem.Component.TrackingType,
                               Unit = i.BorrowItem.Component.Unit,
                               ItemCode = i.BorrowItem.ComponentItem != null ? i.BorrowItem.ComponentItem.ItemCode : null,
                               SerialNumber = i.BorrowItem.ComponentItem != null ? i.BorrowItem.ComponentItem.SerialNumber : null,
                               BorrowedQuantity = i.BorrowItem.Quantity,
                               Quantity = i.Quantity,
                               LineStatus = i.BorrowItem.Status,
                               ReturnedQuantity = i.BorrowItem.ReturnedQuantity,
                               ReturnCondition = i.BorrowItem.ReturnCondition,
                               ReturnedAt = i.BorrowItem.ReturnedAt,
                           })
                           .ToList(),
                   })
                   .FirstOrDefaultAsync(ct)
               ?? throw new ForbiddenException("Bạn không có quyền xem phiếu trả này");
    }

    public async Task<ReturnSlipDetailResponse> ApproveAsync(
        Guid returnSlipId, ApproveReturnSlipRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        var slip = await GetForStaffAsync(returnSlipId, ct);

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockBorrowedRequestAsync(slip.BorrowRequestId, "Đơn mượn không còn đang mượn, không duyệt phiếu trả được", ct);
        var appointmentDate = ToVietnamDateUtc(request.AppointmentDate!.Value);
        await EnsureAppointmentDateAsync(slip.BorrowRequestId, appointmentDate, now, ct);

        await ReviewAsync(returnSlipId, ReturnSlipStatuses.Approved, appointmentDate, userId, CleanText(request.Comment), now, ct);

        await tx.CommitAsync(ct);

        await _notifier.ReturnSlipReviewedAsync(returnSlipId, ct);
        await _changes.BorrowRequestChangedAsync(slip.BorrowRequestId);

        return await GetByIdAsync(returnSlipId, ct);
    }

    public async Task<ReturnSlipDetailResponse> RescheduleAsync(
        Guid returnSlipId, RescheduleReturnSlipRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        var slip = await GetForStaffAsync(returnSlipId, ct);

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockBorrowedRequestAsync(slip.BorrowRequestId, "Đơn mượn không còn đang mượn, không đổi ngày hẹn được", ct);
        var appointmentDate = ToVietnamDateUtc(request.AppointmentDate!.Value);
        await EnsureAppointmentDateAsync(slip.BorrowRequestId, appointmentDate, now, ct);

        var comment = CleanText(request.Comment);
        var moved = await _uow.Repository<BorrowReturnSlip>().Query()
            .Where(s => s.ReturnSlipId == returnSlipId && s.Status == ReturnSlipStatuses.Approved)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.AppointmentDate, appointmentDate)
                .SetProperty(s => s.ReviewedBy, userId)
                .SetProperty(s => s.ReviewedAt, now)
                .SetProperty(s => s.ReviewComment, comment)
                .SetProperty(s => s.UpdatedAt, now), ct);
        if (moved == 0)
            throw new ConflictException("Chỉ đổi ngày hẹn của phiếu trả đã duyệt");

        await tx.CommitAsync(ct);

        await _notifier.ReturnSlipRescheduledAsync(returnSlipId, ct);
        await _changes.BorrowRequestChangedAsync(slip.BorrowRequestId);

        return await GetByIdAsync(returnSlipId, ct);
    }

    public async Task<ReturnSlipDetailResponse> RejectAsync(
        Guid returnSlipId, RejectReturnSlipRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        var slip = await GetForStaffAsync(returnSlipId, ct);

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(slip.BorrowRequestId, ct);
        await ReviewAsync(returnSlipId, ReturnSlipStatuses.Rejected, null, userId, request.Reason.Trim(), now, ct);

        await tx.CommitAsync(ct);

        await _notifier.ReturnSlipReviewedAsync(returnSlipId, ct);
        await _changes.BorrowRequestChangedAsync(slip.BorrowRequestId);

        return await GetByIdAsync(returnSlipId, ct);
    }

    public async Task<ReturnSlipDetailResponse> CancelAsync(Guid returnSlipId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var now = DateTime.UtcNow;

        var borrowRequestId = await _uow.Repository<BorrowReturnSlip>().QueryNoTracking()
                                  .Where(s => s.ReturnSlipId == returnSlipId)
                                  .Select(s => (Guid?)s.BorrowRequestId)
                                  .FirstOrDefaultAsync(ct)
                              ?? throw new NotFoundException("Không tìm thấy phiếu trả");
        await EnsureBorrowerAsync(borrowRequestId, userId, ct);

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await LockRequestAsync(borrowRequestId, ct);
        var cancelled = await _uow.Repository<BorrowReturnSlip>().Query()
            .Where(s => s.ReturnSlipId == returnSlipId && ReturnSlipStatuses.Open.Contains(s.Status))
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, ReturnSlipStatuses.Cancelled)
                .SetProperty(s => s.UpdatedAt, now), ct);
        if (cancelled == 0)
            throw new ConflictException("Chỉ hủy được phiếu trả đang chờ duyệt hoặc đã duyệt");

        await tx.CommitAsync(ct);

        await _notifier.ReturnSlipCancelledAsync(returnSlipId, ct);
        await _changes.BorrowRequestChangedAsync(borrowRequestId);

        return await GetByIdAsync(returnSlipId, ct);
    }

    private sealed record StaffTarget(Guid BorrowRequestId);

    /// <summary>404 when missing, 403 unless Admin or staff of the request's lab.</summary>
    private async Task<StaffTarget> GetForStaffAsync(Guid returnSlipId, CancellationToken ct)
    {
        var slip = await _uow.Repository<BorrowReturnSlip>().QueryNoTracking()
                       .Where(s => s.ReturnSlipId == returnSlipId)
                       .Select(s => new { s.BorrowRequestId, s.BorrowRequest.RoomId })
                       .FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Không tìm thấy phiếu trả");

        if (!_currentUser.IsInRole(RoleNames.Admin) && !await MyRoomIds().ContainsAsync(slip.RoomId, ct))
            throw new ForbiddenException("Chỉ nhân viên phụ trách lab của đơn được xử lý phiếu trả này");

        return new StaffTarget(slip.BorrowRequestId);
    }

    /// <summary>Pending → <paramref name="status"/>; 409 when it was already reviewed, cancelled or closed.</summary>
    private async Task ReviewAsync(
        Guid returnSlipId, string status, DateTime? appointmentDate, Guid userId, string? comment, DateTime now,
        CancellationToken ct)
    {
        var reviewed = await _uow.Repository<BorrowReturnSlip>().Query()
            .Where(s => s.ReturnSlipId == returnSlipId && s.Status == ReturnSlipStatuses.Pending)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, status)
                .SetProperty(s => s.AppointmentDate, appointmentDate)
                .SetProperty(s => s.ReviewedBy, userId)
                .SetProperty(s => s.ReviewedAt, now)
                .SetProperty(s => s.ReviewComment, comment)
                .SetProperty(s => s.UpdatedAt, now), ct);
        if (reviewed == 0)
            throw new ConflictException("Phiếu trả không còn chờ duyệt");
    }

    /// <summary>Only the student who sent the request or the leader of its team.</summary>
    private async Task EnsureBorrowerAsync(Guid borrowRequestId, Guid userId, CancellationToken ct)
    {
        var request = await _uow.Repository<BorrowRequest>().QueryNoTracking()
                          .Where(r => r.BorrowRequestId == borrowRequestId)
                          .Select(r => new
                          {
                              r.RequesterId,
                              LeaderUserId = r.Project != null && r.Project.Team != null
                                  ? (Guid?)r.Project.Team.Leader.UserId
                                  : null,
                          })
                          .FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Không tìm thấy đơn mượn");

        if (request.RequesterId != userId && request.LeaderUserId != userId)
            throw new ForbiddenException("Chỉ người gửi đơn mượn hoặc trưởng nhóm được tạo và hủy phiếu trả");
    }

    /// <summary>Today or later, and not after the due date unless the request is already overdue.</summary>
    private async Task EnsureAppointmentDateAsync(
        Guid borrowRequestId, DateTime appointmentDate, DateTime now, CancellationToken ct)
    {
        var dueDate = await _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .Select(r => r.DueDate)
            .FirstAsync(ct);

        var today = TodayUtc(now);
        var lastDay = ToVietnamDateUtc(dueDate);

        if (appointmentDate < today)
            throw new BadRequestException("Ngày hẹn trả không được ở quá khứ");
        if (lastDay >= today && appointmentDate > lastDay)
            throw new BadRequestException($"Ngày hẹn trả không được sau hạn trả {ToVietnamDateText(lastDay)}");
    }

    /// <summary>The chosen lines: of this request, still out and on no other open slip. Call with the request locked.</summary>
    private async Task<List<BorrowReturnSlipItem>> GetSlipLinesAsync(
        Guid borrowRequestId, CreateReturnSlipRequest request, CancellationToken ct)
    {
        var lineIds = request.Lines.Select(l => l.BorrowItemId).ToList();
        var lines = await _uow.Repository<BorrowItem>().QueryNoTracking()
            .Where(i => i.BorrowRequestId == borrowRequestId && lineIds.Contains(i.BorrowItemId))
            .Select(i => new
            {
                i.BorrowItemId,
                i.Quantity,
                i.Status,
                i.Component.ComponentName,
                i.Component.TrackingType,
                ItemCode = i.ComponentItem != null ? i.ComponentItem.ItemCode : null,
                OpenSlipCode = i.BorrowReturnSlipItems
                    .Where(s => ReturnSlipStatuses.Open.Contains(s.ReturnSlip.Status))
                    .Select(s => s.ReturnSlip.SlipCode)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);
        if (lines.Count != lineIds.Count)
            throw new BadRequestException("Có món không thuộc đơn mượn này");

        var errors = new List<string>();
        var slipLines = new List<BorrowReturnSlipItem>();
        foreach (var line in lines)
        {
            var name = line.ItemCode == null ? line.ComponentName : $"{line.ComponentName} ({line.ItemCode})";
            var quantity = request.Lines.First(l => l.BorrowItemId == line.BorrowItemId).Quantity;

            if (line.Status == BorrowItemStatuses.Pending)
                errors.Add($"{name}: món thay thế chưa nhận nên chưa trả được");
            else if (line.Status != BorrowItemStatuses.Issued)
                errors.Add($"{name}: không còn đang mượn");
            else if (line.OpenSlipCode != null)
                errors.Add($"{name}: đã có trong phiếu trả {line.OpenSlipCode}");
            else if (line.TrackingType == ComponentTrackingTypes.Consumable && (quantity == null || quantity > line.Quantity))
                errors.Add($"{name}: số lượng trả phải từ 0 đến {line.Quantity}");
            else if (line.TrackingType != ComponentTrackingTypes.Consumable && quantity != null && quantity != 1)
                errors.Add($"{name}: hàng theo từng con, số lượng trả là 1");

            slipLines.Add(new BorrowReturnSlipItem { BorrowItemId = line.BorrowItemId, Quantity = quantity ?? 1 });
        }

        if (errors.Count > 0)
            throw new BadRequestException("Phiếu trả chưa hợp lệ", new Dictionary<string, string[]> { ["lines"] = errors.ToArray() });

        return slipLines;
    }

    /// <summary>Locks the request row (no-op UPDATE), the same lock as the other borrowing actions.</summary>
    private Task LockRequestAsync(Guid borrowRequestId, CancellationToken ct)
        => _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, r => r.Status), ct);

    /// <summary>Locks the request row; 409 when the request is not being borrowed.</summary>
    private async Task LockBorrowedRequestAsync(Guid borrowRequestId, string wrongStatusMessage, CancellationToken ct)
    {
        var locked = await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.BorrowRequestId == borrowRequestId && BorrowedStatuses.Contains(r.Status))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, r => r.Status), ct);
        if (locked == 0)
            throw new ConflictException(wrongStatusMessage);
    }

    private async Task<string> GenerateSlipCodeAsync(DateTime now, CancellationToken ct)
    {
        var prefix = $"PT-{now.Add(VietnamUtcOffset):yyyyMMdd}-";
        var todayCodes = await _uow.Repository<BorrowReturnSlip>().QueryNoTracking()
            .Where(s => s.SlipCode.StartsWith(prefix))
            .Select(s => s.SlipCode)
            .ToListAsync(ct);

        var maxNumber = 0;
        foreach (var code in todayCodes)
        {
            if (int.TryParse(code.Substring(prefix.Length), out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"{prefix}{maxNumber + 1:D3}";
    }

    private IQueryable<Guid> MyRoomIds()
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        return _uow.Repository<LabRoomStaff>().QueryNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.RoomId);
    }

    /// <summary>Same people as the borrow request: borrower, team, lecturer of the class, staff of its lab, Admin.</summary>
    private IQueryable<BorrowReturnSlip> VisibleSlips()
    {
        var slips = _uow.Repository<BorrowReturnSlip>().QueryNoTracking();
        if (_currentUser.IsInRole(RoleNames.Admin))
            return slips;

        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var isStaff = _currentUser.HasPermission(PermissionCodes.BorrowIssue);
        var myRooms = MyRoomIds();

        return slips.Where(s =>
            s.BorrowRequest.RequesterId == userId
            || (s.BorrowRequest.Project != null && s.BorrowRequest.Project.Team != null
                && (s.BorrowRequest.Project.Team.Leader.UserId == userId
                    || s.BorrowRequest.Project.Team.TeamMembers.Any(tm =>
                        tm.Student.UserId == userId && tm.Status == TeamMemberStatuses.Accepted)))
            || (s.BorrowRequest.Class != null && s.BorrowRequest.Class.Instructor != null
                && s.BorrowRequest.Class.Instructor.UserId == userId)
            || (isStaff && myRooms.Contains(s.BorrowRequest.RoomId)));
    }

    private static IQueryable<BorrowReturnSlip> Filter(IQueryable<BorrowReturnSlip> slips, ReturnSlipQueryParams query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            slips = slips.Where(s => s.SlipCode.ToLower().Contains(keyword)
                                     || s.BorrowRequest.RequestCode.ToLower().Contains(keyword)
                                     || s.BorrowRequest.Requester.FullName.ToLower().Contains(keyword)
                                     || (s.BorrowRequest.Requester.StudentProfile != null
                                         && s.BorrowRequest.Requester.StudentProfile.StudentCode.ToLower().Contains(keyword))
                                     || (s.BorrowRequest.Project != null
                                         && s.BorrowRequest.Project.Title.ToLower().Contains(keyword)));
        }

        var status = ReturnSlipStatuses.Normalize(query.Status);
        if (status != null)
            slips = slips.Where(s => s.Status == status);

        if (query.BorrowRequestId != null)
            slips = slips.Where(s => s.BorrowRequestId == query.BorrowRequestId);

        if (query.RoomId != null)
            slips = slips.Where(s => s.BorrowRequest.RoomId == query.RoomId);

        if (query.FromDate != null)
        {
            var from = ToVietnamDateUtc(query.FromDate.Value);
            slips = slips.Where(s => s.AppointmentDate >= from);
        }

        if (query.ToDate != null)
        {
            var to = ToVietnamDateUtc(query.ToDate.Value);
            slips = slips.Where(s => s.AppointmentDate <= to);
        }

        return slips;
    }

    private static Task<PagedResult<ReturnSlipResponse>> ToPagedResponses(
        IQueryable<BorrowReturnSlip> slips, PageParams paging, CancellationToken ct)
    {
        var today = TodayUtc(DateTime.UtcNow);
        var result = slips
            .OrderByDescending(s => s.CreatedAt).ThenBy(s => s.ReturnSlipId)
            .Select(s => new ReturnSlipResponse
            {
                ReturnSlipId = s.ReturnSlipId,
                SlipCode = s.SlipCode,
                Status = s.Status,
                IsLate = s.Status == ReturnSlipStatuses.Approved && s.AppointmentDate < today,
                AppointmentDate = s.AppointmentDate,
                BorrowRequestId = s.BorrowRequestId,
                RequestCode = s.BorrowRequest.RequestCode,
                DueDate = s.BorrowRequest.DueDate,
                RequesterId = s.BorrowRequest.RequesterId,
                RequesterName = s.BorrowRequest.Requester.FullName,
                StudentCode = s.BorrowRequest.Requester.StudentProfile != null
                    ? s.BorrowRequest.Requester.StudentProfile.StudentCode
                    : null,
                ClassCode = s.BorrowRequest.Class != null ? s.BorrowRequest.Class.ClassCode : null,
                ProjectTitle = s.BorrowRequest.Project != null ? s.BorrowRequest.Project.Title : null,
                RoomId = s.BorrowRequest.RoomId,
                RoomCode = s.BorrowRequest.Room.RoomCode,
                RoomName = s.BorrowRequest.Room.RoomName,
                ItemCount = s.BorrowReturnSlipItems.Count,
                StillOutCount = s.BorrowRequest.BorrowItems.Count(i => i.Status == BorrowItemStatuses.Issued),
                CreatedAt = s.CreatedAt,
            });

        return PagedResult<ReturnSlipResponse>.CreateAsync(result, paging, ct);
    }

    private static DateTime TodayUtc(DateTime now) => ToVietnamDateUtc(now);

    private static string ToVietnamDateText(DateTime utc) => utc.Add(VietnamUtcOffset).ToString("dd/MM/yyyy");

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

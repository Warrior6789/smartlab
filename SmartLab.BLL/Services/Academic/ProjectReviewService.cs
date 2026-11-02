using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.BLL.Services.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

/// <summary>
/// Every decision first locks the project's Pending borrow request (a no-op UPDATE ... WHERE status = 'Pending'), then
/// reads the current step. Approve, reject and withdraw of the same project therefore run one after another. Lock order is
/// always borrow request → lab (approve only) → project, so they cannot deadlock. A project that only uses self-supplied
/// items has no borrow request: the lecturer of its class decides it directly, and the conditional UPDATE on the project
/// (WHERE status = 'Submitted') makes the first decision or withdrawal win.
/// </summary>
public class ProjectReviewService : IProjectReviewService
{
    private const string AlreadyDecidedMessage = "Project đã được xử lý hoặc đã bị rút lại, vui lòng tải lại";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectService _projectService;
    private readonly IBorrowNotifier _notifier;
    private readonly IProjectNotifier _projectNotifier;
    private readonly IChangeBroadcaster _changes;

    public ProjectReviewService(
        IUnitOfWork uow, ICurrentUserService currentUser, IProjectService projectService, IBorrowNotifier notifier,
        IProjectNotifier projectNotifier, IChangeBroadcaster changes)
    {
        _uow = uow;
        _currentUser = currentUser;
        _projectService = projectService;
        _notifier = notifier;
        _projectNotifier = projectNotifier;
        _changes = changes;
    }

    /// <summary>
    /// Borrow requests waiting for the caller plus submitted projects without a borrow request (self-supplied only).
    /// The two come from different tables, so both are read and merged, oldest first, then paged in memory.
    /// </summary>
    public async Task<PagedResult<PendingApprovalProjectResponse>> GetPendingAsync(
        PendingApprovalQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        string? keyword = null;
        if (!string.IsNullOrWhiteSpace(query.Search))
            keyword = query.Search.Trim().ToLower();

        var approvals = _uow.Repository<BorrowApproval>().QueryNoTracking()
            .Where(a => a.Status == BorrowApprovalStatuses.Pending
                        && a.BorrowRequest.Status == BorrowRequestStatuses.Pending
                        && a.BorrowRequest.Project != null
                        && !a.BorrowRequest.BorrowApprovals.Any(o => o.StepNo < a.StepNo
                                                                    && o.Status != BorrowApprovalStatuses.Approved));

        if (!_currentUser.IsInRole(RoleNames.Admin))
            approvals = approvals.Where(a => a.ApproverId == userId);

        if (keyword != null)
        {
            approvals = approvals.Where(a =>
                a.BorrowRequest.RequestCode.ToLower().Contains(keyword)
                || a.BorrowRequest.Project!.Title.ToLower().Contains(keyword)
                || (a.BorrowRequest.Project.Team != null && a.BorrowRequest.Project.Team.TeamName.ToLower().Contains(keyword)));
        }

        var fromBorrows = await approvals
            .Select(a => new PendingRow
            {
                SortAt = a.BorrowRequest.RequestDate,
                Item = new PendingApprovalProjectResponse
                {
                    ProjectId = a.BorrowRequest.Project!.ProjectId,
                    Title = a.BorrowRequest.Project.Title,
                    TeamName = a.BorrowRequest.Project.Team != null ? a.BorrowRequest.Project.Team.TeamName : null,
                    ClassCode = a.BorrowRequest.Project.Class != null ? a.BorrowRequest.Project.Class.ClassCode : null,
                    OwnerName = a.BorrowRequest.Project.Owner.FullName,
                    SubmittedAt = a.BorrowRequest.Project.SubmittedAt,
                    BorrowRequestId = a.BorrowRequestId,
                    RequestCode = a.BorrowRequest.RequestCode,
                    IsSupplementary = a.BorrowRequest.Project.Status == ProjectStatuses.Approved,
                    Purpose = a.BorrowRequest.Purpose,
                    StepNo = a.StepNo,
                    ApproverRole = a.ApproverRole,
                    ApproverId = a.ApproverId,
                    ApproverName = a.Approver.FullName,
                    RoomCode = a.BorrowRequest.Room.RoomCode,
                    BorrowDate = a.BorrowRequest.BorrowDate,
                    DueDate = a.BorrowRequest.DueDate,
                    ComponentCount = a.BorrowRequest.BorrowItems.Count,
                    HasHighValue = a.BorrowRequest.BorrowItems.Any(i => i.Component.TrackingType == ComponentTrackingTypes.HighValue),
                },
            })
            .ToListAsync(ct);

        var projects = _uow.Repository<Project>().QueryNoTracking()
            .Where(p => p.Status == ProjectStatuses.Submitted
                        && p.Class != null && p.Class.Instructor != null
                        && !p.BorrowRequests.Any(r => r.Status == BorrowRequestStatuses.Pending));

        if (!_currentUser.IsInRole(RoleNames.Admin))
            projects = projects.Where(p => p.Class!.Instructor!.UserId == userId);

        if (keyword != null)
        {
            projects = projects.Where(p =>
                p.Title.ToLower().Contains(keyword)
                || (p.Team != null && p.Team.TeamName.ToLower().Contains(keyword)));
        }

        var fromProjects = await projects
            .Select(p => new PendingRow
            {
                SortAt = p.SubmittedAt ?? p.UpdatedAt,
                Item = new PendingApprovalProjectResponse
                {
                    ProjectId = p.ProjectId,
                    Title = p.Title,
                    TeamName = p.Team != null ? p.Team.TeamName : null,
                    ClassCode = p.Class!.ClassCode,
                    OwnerName = p.Owner.FullName,
                    SubmittedAt = p.SubmittedAt,
                    IsSupplementary = false,
                    Purpose = "Project chỉ dùng linh kiện tự chuẩn bị: " + p.Title,
                    StepNo = 1,
                    ApproverRole = ApproverRoles.Lecturer,
                    ApproverId = p.Class.Instructor!.UserId,
                    ApproverName = p.Class.Instructor.User.FullName,
                    ComponentCount = 0,
                    HasHighValue = false,
                },
            })
            .ToListAsync(ct);

        var all = fromBorrows.Concat(fromProjects)
            .OrderBy(r => r.SortAt).ThenBy(r => r.Item.ProjectId)
            .Select(r => r.Item)
            .ToList();

        var pageSize = Math.Clamp(paging.Size, 1, 100);
        var pageNumber = Math.Clamp(paging.Page, 1, int.MaxValue / pageSize);

        return new PagedResult<PendingApprovalProjectResponse>
        {
            Items = all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = all.Count,
        };
    }

    private sealed class PendingRow
    {
        public DateTime SortAt { get; init; }

        public PendingApprovalProjectResponse Item { get; init; } = null!;
    }

    public async Task<ProjectDetailResponse> ApproveAsync(Guid projectId, ApproveProjectRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var comment = CleanText(request.Comment);
        var now = DateTime.UtcNow;

        if (!await HasPendingBorrowAsync(projectId, ct))
            return await ReviewWithoutBorrowAsync(projectId, ProjectStatuses.Approved, userId, comment, now, ct);

        await using var tx = await _uow.BeginTransactionAsync(ct);

        var step = await LockAndGetCurrentStepAsync(projectId, userId, ct);

        await DecideStepAsync(step.ApprovalId, BorrowApprovalStatuses.Approved, userId, comment, now, ct);

        var allAtPickup = false;
        if (step.IsLast)
        {
            allAtPickup = await AllocateSourcesAsync(step, userId, now, ct);

            var status = BorrowRequestStatuses.Approved;
            if (allAtPickup)
                status = BorrowRequestStatuses.Ready;

            await _uow.Repository<BorrowRequest>().Query()
                .Where(r => r.BorrowRequestId == step.BorrowRequestId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, status)
                    .SetProperty(r => r.ApprovedAt, now), ct);

            if (!step.IsSupplementary)
                await ReviewProjectAsync(projectId, ProjectStatuses.Approved, userId, comment, now, ct);
        }

        await tx.CommitAsync(ct);

        if (step.IsLast)
        {
            if (allAtPickup)
                await _notifier.ReadyAsync(step.BorrowRequestId, ct);
            else
                await _notifier.ApprovedAsync(step.BorrowRequestId, ct);

            await _notifier.StaffApprovedAsync(step.BorrowRequestId, ct);
        }

        await _changes.BorrowRequestChangedAsync(step.BorrowRequestId);

        return await _projectService.GetProjectByIdAsync(projectId, ct);
    }

    public async Task<ProjectDetailResponse> RejectAsync(Guid projectId, RejectProjectRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var reason = request.Reason.Trim();
        var now = DateTime.UtcNow;

        if (!await HasPendingBorrowAsync(projectId, ct))
            return await ReviewWithoutBorrowAsync(projectId, ProjectStatuses.Rejected, userId, reason, now, ct);

        await using var tx = await _uow.BeginTransactionAsync(ct);

        var step = await LockAndGetCurrentStepAsync(projectId, userId, ct);

        await DecideStepAsync(step.ApprovalId, BorrowApprovalStatuses.Rejected, userId, reason, now, ct);

        await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.BorrowRequestId == step.BorrowRequestId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, BorrowRequestStatuses.Rejected)
                .SetProperty(r => r.RejectReason, reason), ct);

        if (!step.IsSupplementary)
            await ReviewProjectAsync(projectId, ProjectStatuses.Rejected, userId, reason, now, ct);

        await tx.CommitAsync(ct);

        await _notifier.RejectedAsync(step.BorrowRequestId, ct);
        await _changes.BorrowRequestChangedAsync(step.BorrowRequestId);

        return await _projectService.GetProjectByIdAsync(projectId, ct);
    }

    private Task<bool> HasPendingBorrowAsync(Guid projectId, CancellationToken ct)
        => _uow.Repository<BorrowRequest>().QueryNoTracking()
            .AnyAsync(r => r.ProjectId == projectId && r.Status == BorrowRequestStatuses.Pending, ct);

    /// <summary>
    /// Approves or rejects a submitted project that has no borrow request (self-supplied items only). Only the lecturer
    /// of the class, or an Admin standing in, may decide it.
    /// </summary>
    private async Task<ProjectDetailResponse> ReviewWithoutBorrowAsync(
        Guid projectId, string status, Guid userId, string? comment, DateTime now, CancellationToken ct)
    {
        var project = await _uow.Repository<Project>().QueryNoTracking()
                          .Where(p => p.ProjectId == projectId)
                          .Select(p => new
                          {
                              p.Status,
                              LecturerUserId = p.Class != null && p.Class.Instructor != null
                                  ? (Guid?)p.Class.Instructor.UserId
                                  : null,
                          })
                          .FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Không tìm thấy project");

        if (project.Status != ProjectStatuses.Submitted)
            throw new ConflictException("Project không ở trạng thái chờ duyệt");
        if (project.LecturerUserId != userId && !_currentUser.IsInRole(RoleNames.Admin))
            throw new ForbiddenException("Chỉ giảng viên của lớp được duyệt project này");

        await ReviewProjectAsync(projectId, status, userId, comment, now, ct);

        await _projectNotifier.ReviewedAsync(projectId, ct);
        await _changes.ProjectChangedAsync(projectId);

        return await _projectService.GetProjectByIdAsync(projectId, ct);
    }

    /// <param name="IsSupplementary">
    /// The project is already Approved and this request borrows more: the decision touches only the borrow request.
    /// </param>
    private sealed record CurrentStep(Guid ApprovalId, Guid BorrowRequestId, Guid RoomId, bool IsLast, bool IsSupplementary);

    /// <summary>
    /// Approving reserves stock, so the checks of submitting run again (things may have changed since): the pickup lab is
    /// still active, every component is still in use, and all labs together can still lend every line. Then each line gets
    /// its source lab: the pickup lab first, the rest from the labs with the most left (a line may be split over several
    /// labs). Lines taken from the pickup lab count as received. Every lab is locked first, so two approvals cannot both
    /// take the last units. Returns true when everything is already at the pickup lab. Rejecting is always allowed.
    /// </summary>
    private async Task<bool> AllocateSourcesAsync(CurrentStep step, Guid userId, DateTime now, CancellationToken ct)
    {
        await RoomStock.LockAllRoomsAsync(_uow, ct);

        var pickupActive = await _uow.Repository<LabRoom>().QueryNoTracking()
            .AnyAsync(r => r.RoomId == step.RoomId && r.Status == LabRoomStatuses.Active, ct);
        if (!pickupActive)
            throw new BadRequestException("Lab nhận đồ của đơn mượn đã ngừng hoạt động, vui lòng báo Admin.");

        var lines = await _uow.Repository<BorrowItem>().Query()
            .Include(i => i.Component)
            .Where(i => i.BorrowRequestId == step.BorrowRequestId)
            .OrderBy(i => i.Component.ComponentCode)
            .ToListAsync(ct);

        var inactive = lines.Where(l => !l.Component.IsActive).Select(l => l.Component.ComponentName).ToList();
        if (inactive.Count > 0)
            throw new BadRequestException(
                $"Linh kiện đã ngừng sử dụng: {string.Join(", ", inactive)}. Hãy từ chối để nhóm đổi linh kiện khác.");

        var ids = lines.Select(l => l.ComponentId).Distinct().ToList();
        var availableByRoom = await RoomStock.GetAvailableByRoomAsync(_uow, ids, ct);

        var plans = new List<(BorrowItem Line, List<(Guid RoomId, int Quantity)> Parts)>();
        var shortages = new List<string>();
        foreach (var line in lines)
        {
            var parts = TakeFromRooms(availableByRoom, step.RoomId, line.ComponentId, line.Quantity);
            var taken = parts.Sum(p => p.Quantity);
            if (taken < line.Quantity)
                shortages.Add($"{line.Component.ComponentName} (cần {line.Quantity}, còn {taken})");

            plans.Add((line, parts));
        }

        if (shortages.Count > 0)
            throw new BadRequestException(
                $"Kho không còn đủ linh kiện (đã trừ các đơn đã duyệt chưa cấp): {string.Join("; ", shortages)}. " +
                "Hãy từ chối để nhóm giảm số lượng hoặc đổi linh kiện.");

        var borrowItems = _uow.Repository<BorrowItem>();
        var allAtPickup = true;
        foreach (var (line, parts) in plans)
        {
            for (var index = 0; index < parts.Count; index++)
            {
                var (roomId, quantity) = parts[index];

                var target = line;
                if (index > 0)
                {
                    target = new BorrowItem
                    {
                        BorrowRequestId = line.BorrowRequestId,
                        ComponentId = line.ComponentId,
                        Status = BorrowItemStatuses.Pending,
                    };
                    await borrowItems.AddAsync(target, ct);
                }

                target.Quantity = quantity;
                target.SourceRoomId = roomId;
                if (roomId == step.RoomId)
                {
                    target.ReceivedAt = now;
                    target.ReceivedBy = userId;
                }
                else
                {
                    allAtPickup = false;
                }
            }
        }

        await _uow.SaveChangesAsync(ct);
        return allAtPickup;
    }

    /// <summary>
    /// Takes up to <paramref name="quantity"/> of a component: the pickup lab first, then the labs with the most left
    /// (ties by lab id so the result is stable). Taken amounts are removed from <paramref name="availableByRoom"/>.
    /// </summary>
    private static List<(Guid RoomId, int Quantity)> TakeFromRooms(
        Dictionary<Guid, Dictionary<Guid, int>> availableByRoom, Guid pickupRoomId, Guid componentId, int quantity)
    {
        var order = availableByRoom
            .Where(r => r.Key != pickupRoomId && r.Value.GetValueOrDefault(componentId) > 0)
            .OrderByDescending(r => r.Value[componentId]).ThenBy(r => r.Key)
            .Select(r => r.Key)
            .ToList();
        if (availableByRoom.TryGetValue(pickupRoomId, out var pickup) && pickup.GetValueOrDefault(componentId) > 0)
            order.Insert(0, pickupRoomId);

        var parts = new List<(Guid RoomId, int Quantity)>();
        var left = quantity;
        foreach (var roomId in order)
        {
            if (left == 0)
                break;

            var room = availableByRoom[roomId];
            var take = Math.Min(left, room[componentId]);
            room[componentId] -= take;
            left -= take;
            parts.Add((roomId, take));
        }

        return parts;
    }

    /// <summary>
    /// Locks the project's Pending borrow request, then returns its lowest Pending step after checking the caller may
    /// decide it (the assigned approver, or any Admin standing in).
    /// </summary>
    private async Task<CurrentStep> LockAndGetCurrentStepAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var locked = await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.ProjectId == projectId && r.Status == BorrowRequestStatuses.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, BorrowRequestStatuses.Pending), ct);
        if (locked == 0)
        {
            if (!await _uow.Repository<Project>().QueryNoTracking().AnyAsync(p => p.ProjectId == projectId, ct))
                throw new NotFoundException("Không tìm thấy project");

            throw new ConflictException("Project không ở trạng thái chờ duyệt");
        }

        var pendingSteps = await _uow.Repository<BorrowApproval>().QueryNoTracking()
            .Where(a => a.BorrowRequest.ProjectId == projectId
                        && a.BorrowRequest.Status == BorrowRequestStatuses.Pending
                        && a.Status == BorrowApprovalStatuses.Pending)
            .OrderBy(a => a.StepNo)
            .Select(a => new
            {
                a.ApprovalId,
                a.ApproverId,
                a.BorrowRequestId,
                a.BorrowRequest.RoomId,
                IsSupplementary = a.BorrowRequest.Project!.Status == ProjectStatuses.Approved,
            })
            .ToListAsync(ct);
        if (pendingSteps.Count == 0)
            throw new ConflictException(AlreadyDecidedMessage);

        var current = pendingSteps[0];
        if (current.ApproverId != userId && !_currentUser.IsInRole(RoleNames.Admin))
            throw new ForbiddenException("Project này chưa đến lượt bạn duyệt hoặc bạn không phải người duyệt");

        return new CurrentStep(
            current.ApprovalId, current.BorrowRequestId, current.RoomId, pendingSteps.Count == 1, current.IsSupplementary);
    }

    /// <summary>Records the decision; ApproverId becomes the user who decided (an Admin standing in replaces the assignee).</summary>
    private async Task DecideStepAsync(Guid approvalId, string status, Guid userId, string? comment, DateTime now, CancellationToken ct)
    {
        var updated = await _uow.Repository<BorrowApproval>().Query()
            .Where(a => a.ApprovalId == approvalId && a.Status == BorrowApprovalStatuses.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, status)
                .SetProperty(a => a.ApproverId, userId)
                .SetProperty(a => a.Comment, comment)
                .SetProperty(a => a.DecidedAt, now), ct);
        if (updated == 0)
            throw new ConflictException(AlreadyDecidedMessage);
    }

    /// <summary>The final decision: Submitted → Approved / Rejected, with who reviewed, when and the comment.</summary>
    private async Task ReviewProjectAsync(Guid projectId, string status, Guid userId, string? comment, DateTime now, CancellationToken ct)
    {
        var updated = await _uow.Repository<Project>().Query()
            .Where(p => p.ProjectId == projectId && p.Status == ProjectStatuses.Submitted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, status)
                .SetProperty(p => p.ReviewedBy, userId)
                .SetProperty(p => p.ReviewedAt, now)
                .SetProperty(p => p.ReviewComment, comment)
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (updated == 0)
            throw new ConflictException(AlreadyDecidedMessage);
    }

    private static string? CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Trim();
    }
}

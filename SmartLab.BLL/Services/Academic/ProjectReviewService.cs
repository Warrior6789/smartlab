using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.BLL.Services.Borrowing;
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
                    PendingRequestCount = a.BorrowRequest.Project.BorrowRequests.Count(r => r.Status == BorrowRequestStatuses.Pending),
                    HasHighValue = a.BorrowRequest.BorrowItems.Any(i => i.Component.TrackingType == ComponentTrackingTypes.HighValue),
                },
            })
            .ToListAsync(ct);

        var splitProjectIds = fromBorrows.Where(r => r.Item.PendingRequestCount > 1).Select(r => r.Item.ProjectId).Distinct().ToList();
        if (splitProjectIds.Count > 0)
        {
            var labsByProject = (await _uow.Repository<BorrowRequest>().QueryNoTracking()
                    .Where(r => r.ProjectId != null && splitProjectIds.Contains(r.ProjectId.Value) && r.Status == BorrowRequestStatuses.Pending)
                    .Select(r => new { ProjectId = r.ProjectId!.Value, r.Room.RoomCode })
                    .ToListAsync(ct))
                .GroupBy(r => r.ProjectId)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(r => r.RoomCode).OrderBy(c => c, StringComparer.Ordinal)));
            foreach (var row in fromBorrows)
                row.Item.PendingRoomCodes = labsByProject.GetValueOrDefault(row.Item.ProjectId);
        }

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
        {
            if (request.Adjustments.Count > 0)
                throw new BadRequestException("Project không mượn linh kiện lab nên không có số lượng để điều chỉnh");
            return await ReviewWithoutBorrowAsync(projectId, ProjectStatuses.Approved, userId, comment, now, ct);
        }

        await using var tx = await _uow.BeginTransactionAsync(ct);

        var steps = await LockAndGetPendingStepsAsync(projectId, userId, ct);
        var isSupplementary = steps[0].IsSupplementary;

        var adjusted = await ApplyAdjustmentsAsync(projectId, steps, request.Adjustments, ct);
        if (adjusted != null)
            comment = $"{comment} (Điều chỉnh: {adjusted})";

        foreach (var step in steps)
        {
            await DecideStepAsync(step.ApprovalId, BorrowApprovalStatuses.Approved, userId,
                Shorten(comment, ApprovalCommentMaxLength), now, ct);
        }

        var finished = steps.Where(s => s.IsLast).ToList();
        if (finished.Count > 0)
        {
            await CheckStockAsync(finished, ct);

            // Ready only comes from the lab: see IBorrowRequestService.LabAcceptAsync.
            var finishedIds = finished.Select(s => s.BorrowRequestId).ToList();
            await _uow.Repository<BorrowRequest>().Query()
                .Where(r => finishedIds.Contains(r.BorrowRequestId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, BorrowRequestStatuses.Approved)
                    .SetProperty(r => r.ApprovedAt, now), ct);
        }

        var projectDecided = false;
        if (!isSupplementary)
            projectDecided = await FinishProjectReviewAsync(projectId, userId, Shorten(comment, ReviewCommentMaxLength), now, ct);

        await tx.CommitAsync(ct);

        foreach (var step in finished)
        {
            await _notifier.ApprovedAsync(step.BorrowRequestId, ct);
            await _notifier.StaffApprovedAsync(step.BorrowRequestId, ct);
        }
        if (projectDecided)
            await _changes.ProjectChangedAsync(projectId);

        foreach (var step in steps)
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

        var steps = await LockAndGetPendingStepsAsync(projectId, userId, ct);

        foreach (var step in steps)
            await DecideStepAsync(step.ApprovalId, BorrowApprovalStatuses.Rejected, userId, reason, now, ct);

        var requestIds = steps.Select(s => s.BorrowRequestId).ToList();
        await _uow.Repository<BorrowRequest>().Query()
            .Where(r => requestIds.Contains(r.BorrowRequestId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, BorrowRequestStatuses.Rejected)
                .SetProperty(r => r.RejectReason, reason), ct);

        var projectDecided = false;
        if (!steps[0].IsSupplementary)
            projectDecided = await FinishProjectReviewAsync(projectId, userId, reason, now, ct);

        await tx.CommitAsync(ct);

        foreach (var step in steps)
            await _notifier.RejectedAsync(step.BorrowRequestId, ct);
        if (projectDecided)
            await _changes.ProjectChangedAsync(projectId);
        foreach (var step in steps)
            await _changes.BorrowRequestChangedAsync(step.BorrowRequestId);

        return await _projectService.GetProjectByIdAsync(projectId, ct);
    }

    /// <summary>borrow_approvals.comment and projects.review_comment column sizes.</summary>
    private const int ApprovalCommentMaxLength = 255;
    private const int ReviewCommentMaxLength = 500;

    private static string? Shorten(string? text, int maxLength)
    {
        if (text == null || text.Length <= maxLength)
            return text;
        return text.Substring(0, maxLength - 1) + "…";
    }

    /// <summary>
    /// Lowers the quantities the lecturer changed across all the project's pending lab requests (0 removes the line) before stock is checked, so a lab that cannot lend
    /// the full amount can still lend the lowered one. A first request also updates the project's component list, so the
    /// project shows what was approved. Returns a short "name a→b" summary for the team, or null when nothing changed.
    /// </summary>
    private async Task<string?> ApplyAdjustmentsAsync(
        Guid projectId, List<CurrentStep> steps, List<ApproveQuantityAdjustmentRequest> adjustments, CancellationToken ct)
    {
        if (adjustments.Count == 0)
            return null;

        var requestIds = steps.Select(s => s.BorrowRequestId).ToList();
        var lines = await _uow.Repository<BorrowItem>().Query()
            .Include(i => i.Component)
            .Where(i => requestIds.Contains(i.BorrowRequestId))
            .ToListAsync(ct);
        var projectLines = new List<ProjectComponent>();
        if (!steps[0].IsSupplementary)
        {
            projectLines = await _uow.Repository<ProjectComponent>().Query()
                .Where(pc => pc.ProjectId == projectId)
                .ToListAsync(ct);
        }

        var changes = new List<string>();
        foreach (var adjustment in adjustments)
        {
            var line = lines.FirstOrDefault(l => l.ComponentId == adjustment.ComponentId)
                       ?? throw new BadRequestException("Có linh kiện điều chỉnh không nằm trong đơn mượn");
            var quantity = adjustment.Quantity ?? throw new BadRequestException("Số lượng duyệt là bắt buộc");
            if (quantity > line.Quantity)
                throw new BadRequestException(
                    $"{line.Component.ComponentName}: chỉ được giảm số lượng (nhóm xin {line.Quantity}). Muốn tăng thì nhóm rút đơn và gửi lại.");
            if (quantity == line.Quantity)
                continue;

            changes.Add($"{line.Component.ComponentName} {line.Quantity}→{quantity}");

            var projectLine = projectLines.FirstOrDefault(pc => pc.ComponentId == adjustment.ComponentId);
            if (quantity == 0)
            {
                _uow.Repository<BorrowItem>().Remove(line);
                lines.Remove(line);
                if (projectLine != null)
                    _uow.Repository<ProjectComponent>().Remove(projectLine);
            }
            else
            {
                line.Quantity = quantity;
                if (projectLine != null)
                    projectLine.Quantity = quantity;
            }
        }

        var emptiedRoomIds = steps
            .Where(s => !lines.Any(l => l.BorrowRequestId == s.BorrowRequestId))
            .Select(s => s.RoomId)
            .ToList();
        if (emptiedRoomIds.Count > 0)
        {
            var roomCodes = await _uow.Repository<LabRoom>().QueryNoTracking()
                .Where(r => emptiedRoomIds.Contains(r.RoomId))
                .OrderBy(r => r.RoomCode)
                .Select(r => r.RoomCode)
                .ToListAsync(ct);
            throw new BadRequestException(
                $"Không thể bỏ hết linh kiện của đơn {string.Join(", ", roomCodes)}. Muốn bỏ cả lab đó thì từ chối để nhóm sửa lại.");
        }
        if (changes.Count == 0)
            return null;

        await _uow.SaveChangesAsync(ct);
        return string.Join(", ", changes);
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
    /// Approving reserves stock, so the checks of submitting run again (things may have changed since): every component is
    /// still in use, and each request's own lab can still lend every line. Labs never pass components to each other, so
    /// each lab's staff can accept its request right away. Every lab is locked first, so two approvals cannot both take
    /// the last units. All shortages of all labs are reported together. Rejecting is always allowed.
    /// </summary>
    private async Task CheckStockAsync(List<CurrentStep> steps, CancellationToken ct)
    {
        await RoomStock.LockAllRoomsAsync(_uow, ct);

        var roomByRequest = steps.ToDictionary(s => s.BorrowRequestId, s => s.RoomId);
        var requestIds = roomByRequest.Keys.ToList();
        var lines = await _uow.Repository<BorrowItem>().Query()
            .Include(i => i.Component)
            .Where(i => requestIds.Contains(i.BorrowRequestId))
            .OrderBy(i => i.Component.ComponentCode)
            .ToListAsync(ct);

        var inactive = lines.Where(l => !l.Component.IsActive).Select(l => l.Component.ComponentName).Distinct().ToList();
        if (inactive.Count > 0)
            throw new BadRequestException(
                $"Linh kiện đã ngừng sử dụng: {string.Join(", ", inactive)}. Hãy từ chối để nhóm đổi linh kiện khác.");

        var ids = lines.Select(l => l.ComponentId).Distinct().ToList();
        var availableByRoom = await RoomStock.GetAvailableByRoomAsync(_uow, ids, ct);

        var shortagesByRoom = new Dictionary<Guid, List<string>>();
        foreach (var line in lines)
        {
            var roomId = roomByRequest[line.BorrowRequestId];
            if (!availableByRoom.TryGetValue(roomId, out var roomStock))
            {
                roomStock = new Dictionary<Guid, int>();
                availableByRoom[roomId] = roomStock;
            }

            var left = roomStock.GetValueOrDefault(line.ComponentId);
            if (left < line.Quantity)
            {
                if (!shortagesByRoom.TryGetValue(roomId, out var shortages))
                {
                    shortages = new List<string>();
                    shortagesByRoom[roomId] = shortages;
                }
                shortages.Add($"{line.Component.ComponentName} (cần {line.Quantity}, lab còn {left})");
            }

            roomStock[line.ComponentId] = Math.Max(0, left - line.Quantity);
        }

        if (shortagesByRoom.Count == 0)
            return;

        var shortRoomIds = shortagesByRoom.Keys.ToList();
        var roomCodes = await _uow.Repository<LabRoom>().QueryNoTracking()
            .Where(r => shortRoomIds.Contains(r.RoomId))
            .ToDictionaryAsync(r => r.RoomId, r => r.RoomCode, ct);
        var parts = shortagesByRoom
            .OrderBy(e => roomCodes[e.Key], StringComparer.Ordinal)
            .Select(e => $"{roomCodes[e.Key]} thiếu {string.Join(", ", e.Value)}");
        throw new BadRequestException(
            $"Chưa duyệt được: {string.Join("; ", parts)} (đã trừ các đơn đã duyệt chưa cấp). " +
            "Các lab không chuyển đồ cho nhau: hãy giảm số lượng khi duyệt, hoặc từ chối để nhóm đưa món thiếu vào danh sách tự mua.");
    }

    /// <summary>
    /// Locks the project's Pending borrow requests (one per lab), then returns the lowest Pending step of each after
    /// checking the caller may decide them (the assigned approver, or any Admin standing in). The lecturer decides the
    /// whole project at once, so every pending lab request is approved or rejected together.
    /// </summary>
    private async Task<List<CurrentStep>> LockAndGetPendingStepsAsync(Guid projectId, Guid userId, CancellationToken ct)
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

        var steps = new List<CurrentStep>();
        foreach (var group in pendingSteps.GroupBy(s => s.BorrowRequestId))
        {
            var current = group.First();
            if (current.ApproverId != userId && !_currentUser.IsInRole(RoleNames.Admin))
                throw new ForbiddenException("Project này chưa đến lượt bạn duyệt hoặc bạn không phải người duyệt");

            steps.Add(new CurrentStep(
                current.ApprovalId, current.BorrowRequestId, current.RoomId, group.Count() == 1, current.IsSupplementary));
        }

        return steps;
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

    /// <summary>
    /// A first submission is decided once none of its lab requests is pending any more: Approved when at least one lab
    /// request was approved (the team borrows the rest later), Rejected when every one was rejected. Returns true when the
    /// project was decided now.
    /// </summary>
    private async Task<bool> FinishProjectReviewAsync(Guid projectId, Guid userId, string? comment, DateTime now, CancellationToken ct)
    {
        var requests = _uow.Repository<BorrowRequest>().QueryNoTracking().Where(r => r.ProjectId == projectId);
        if (await requests.AnyAsync(r => r.Status == BorrowRequestStatuses.Pending, ct))
            return false;

        var anyApproved = await requests.AnyAsync(r => r.Status != BorrowRequestStatuses.Rejected
                                                       && r.Status != BorrowRequestStatuses.Cancelled, ct);
        var status = ProjectStatuses.Rejected;
        if (anyApproved)
            status = ProjectStatuses.Approved;

        await ReviewProjectAsync(projectId, status, userId, comment, now, ct);
        return true;
    }

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

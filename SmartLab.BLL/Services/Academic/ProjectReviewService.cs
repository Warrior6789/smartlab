using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Services.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

/// <summary>
/// Every decision first locks the project's Pending borrow request (a no-op UPDATE ... WHERE status = 'Pending'), then
/// reads the current step. Approve, reject and withdraw of the same project therefore run one after another. Lock order is
/// always borrow request → lab (approve only) → project, so they cannot deadlock.
/// </summary>
public class ProjectReviewService : IProjectReviewService
{
    private const string AlreadyDecidedMessage = "Project đã được xử lý hoặc đã bị rút lại, vui lòng tải lại";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectService _projectService;

    public ProjectReviewService(IUnitOfWork uow, ICurrentUserService currentUser, IProjectService projectService)
    {
        _uow = uow;
        _currentUser = currentUser;
        _projectService = projectService;
    }

    public Task<PagedResult<PendingApprovalProjectResponse>> GetPendingAsync(
        PendingApprovalQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var approvals = _uow.Repository<BorrowApproval>().QueryNoTracking()
            .Where(a => a.Status == BorrowApprovalStatuses.Pending
                        && a.BorrowRequest.Status == BorrowRequestStatuses.Pending
                        && a.BorrowRequest.Project != null
                        && !a.BorrowRequest.BorrowApprovals.Any(o => o.StepNo < a.StepNo
                                                                    && o.Status != BorrowApprovalStatuses.Approved));

        if (!_currentUser.IsInRole(RoleNames.Admin))
            approvals = approvals.Where(a => a.ApproverId == userId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            approvals = approvals.Where(a =>
                a.BorrowRequest.RequestCode.ToLower().Contains(keyword)
                || a.BorrowRequest.Project!.Title.ToLower().Contains(keyword)
                || (a.BorrowRequest.Project.Team != null && a.BorrowRequest.Project.Team.TeamName.ToLower().Contains(keyword)));
        }

        var result = approvals
            .OrderBy(a => a.BorrowRequest.RequestDate).ThenBy(a => a.ApprovalId)
            .Select(a => new PendingApprovalProjectResponse
            {
                ProjectId = a.BorrowRequest.Project!.ProjectId,
                Title = a.BorrowRequest.Project.Title,
                TeamName = a.BorrowRequest.Project.Team != null ? a.BorrowRequest.Project.Team.TeamName : null,
                ClassCode = a.BorrowRequest.Project.Class != null ? a.BorrowRequest.Project.Class.ClassCode : null,
                OwnerName = a.BorrowRequest.Project.Owner.FullName,
                SubmittedAt = a.BorrowRequest.Project.SubmittedAt,
                BorrowRequestId = a.BorrowRequestId,
                RequestCode = a.BorrowRequest.RequestCode,
                StepNo = a.StepNo,
                ApproverRole = a.ApproverRole,
                ApproverId = a.ApproverId,
                ApproverName = a.Approver.FullName,
                RoomCode = a.BorrowRequest.Room.RoomCode,
                BorrowDate = a.BorrowRequest.BorrowDate,
                DueDate = a.BorrowRequest.DueDate,
                ComponentCount = a.BorrowRequest.BorrowItems.Count,
                HasHighValue = a.BorrowRequest.BorrowItems.Any(i => i.Component.TrackingType == ComponentTrackingTypes.HighValue),
            });

        return PagedResult<PendingApprovalProjectResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<ProjectDetailResponse> ApproveAsync(Guid projectId, ApproveProjectRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var comment = CleanText(request.Comment);
        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        var step = await LockAndGetCurrentStepAsync(projectId, userId, ct);
        await EnsureRoomCanLendAsync(step, ct);

        await DecideStepAsync(step.ApprovalId, BorrowApprovalStatuses.Approved, userId, comment, now, ct);

        if (step.IsLast)
        {
            await _uow.Repository<BorrowRequest>().Query()
                .Where(r => r.BorrowRequestId == step.BorrowRequestId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, BorrowRequestStatuses.Approved)
                    .SetProperty(r => r.ApprovedAt, now), ct);

            await ReviewProjectAsync(projectId, ProjectStatuses.Approved, userId, comment, now, ct);
        }

        await tx.CommitAsync(ct);

        return await _projectService.GetProjectByIdAsync(projectId, ct);
    }

    public async Task<ProjectDetailResponse> RejectAsync(Guid projectId, RejectProjectRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var reason = request.Reason.Trim();
        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        var step = await LockAndGetCurrentStepAsync(projectId, userId, ct);

        await DecideStepAsync(step.ApprovalId, BorrowApprovalStatuses.Rejected, userId, reason, now, ct);

        await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.BorrowRequestId == step.BorrowRequestId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, BorrowRequestStatuses.Rejected)
                .SetProperty(r => r.RejectReason, reason), ct);

        await ReviewProjectAsync(projectId, ProjectStatuses.Rejected, userId, reason, now, ct);

        await tx.CommitAsync(ct);

        return await _projectService.GetProjectByIdAsync(projectId, ct);
    }

    private sealed record CurrentStep(Guid ApprovalId, Guid BorrowRequestId, Guid RoomId, bool IsLast);

    /// <summary>
    /// Approving reserves the lab's stock, so the same checks as submitting are run again (things may have changed since):
    /// the lab is still active, every component is still in use, and the lab can still lend every line after the requests
    /// it already approved. The lab row is locked first, so two approvals in the same lab run one after another and cannot
    /// both take the last units. Rejecting is always allowed.
    /// </summary>
    private async Task EnsureRoomCanLendAsync(CurrentStep step, CancellationToken ct)
    {
        await RoomStock.LockRoomAsync(_uow, step.RoomId, ct);

        var roomActive = await _uow.Repository<LabRoom>().QueryNoTracking()
            .AnyAsync(r => r.RoomId == step.RoomId && r.Status == LabRoomStatuses.Active, ct);
        if (!roomActive)
            throw new BadRequestException("Lab của đơn mượn đã ngừng hoạt động. Hãy từ chối để nhóm chọn lab khác.");

        var lines = await _uow.Repository<BorrowItem>().QueryNoTracking()
            .Where(i => i.BorrowRequestId == step.BorrowRequestId)
            .GroupBy(i => new { i.ComponentId, i.Component.ComponentName, i.Component.IsActive })
            .Select(g => new { g.Key.ComponentId, g.Key.ComponentName, g.Key.IsActive, Quantity = g.Sum(i => i.Quantity) })
            .ToListAsync(ct);

        var inactive = lines.Where(l => !l.IsActive).Select(l => l.ComponentName).ToList();
        if (inactive.Count > 0)
            throw new BadRequestException(
                $"Linh kiện đã ngừng sử dụng: {string.Join(", ", inactive)}. Hãy từ chối để nhóm đổi linh kiện khác.");

        var ids = lines.Select(l => l.ComponentId).ToList();
        var available = await RoomStock.GetAvailableAsync(_uow, step.RoomId, ids, ct);

        var shortages = new List<string>();
        foreach (var line in lines)
        {
            var left = available.GetValueOrDefault(line.ComponentId);
            if (left < line.Quantity)
                shortages.Add($"{line.ComponentName} (cần {line.Quantity}, còn {left})");
        }

        if (shortages.Count > 0)
            throw new BadRequestException(
                $"Lab không còn đủ linh kiện (đã trừ các đơn đã duyệt chưa cấp): {string.Join("; ", shortages)}. " +
                "Hãy từ chối để nhóm đổi lab hoặc giảm số lượng.");
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
            .Select(a => new { a.ApprovalId, a.ApproverId, a.BorrowRequestId, a.BorrowRequest.RoomId })
            .ToListAsync(ct);
        if (pendingSteps.Count == 0)
            throw new ConflictException(AlreadyDecidedMessage);

        var current = pendingSteps[0];
        if (current.ApproverId != userId && !_currentUser.IsInRole(RoleNames.Admin))
            throw new ForbiddenException("Project này chưa đến lượt bạn duyệt hoặc bạn không phải người duyệt");

        return new CurrentStep(current.ApprovalId, current.BorrowRequestId, current.RoomId, pendingSteps.Count == 1);
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

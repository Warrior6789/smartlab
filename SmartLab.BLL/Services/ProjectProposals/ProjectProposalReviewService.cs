using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ProjectProposals;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.ProjectProposals;
using SmartLab.BLL.Mappings.ProjectProposals;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.ProjectProposals;

public class ProjectProposalReviewService : IProjectProposalReviewService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IComponentReservationService _reservationService;

    public ProjectProposalReviewService(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IComponentReservationService reservationService)
    {
        _uow = uow;
        _currentUser = currentUser;
        _reservationService = reservationService;
    }

    public async Task<PagedResult<ProjectProposalReviewItemResponse>> GetAssignedAsync(
        PageParams paging, CancellationToken ct = default)
    {
        var instructor = await GetCurrentInstructorAsync(ct);

        var proposals = _uow.Repository<Project>().QueryNoTracking()
            .Where(p => p.ClassId.HasValue &&
                        p.TeamId.HasValue &&
                        p.Class!.InstructorId == instructor.InstructorProfileId &&
                        p.Status == ProjectStatuses.Submitted &&
                        p.BorrowRequests.Any(br => br.Status == BorrowRequestStatuses.Pending))
            .OrderBy(p => p.BorrowRequests
                .Where(br => br.Status == BorrowRequestStatuses.Pending)
                .Max(br => br.RequestDate))
            .Select(p => new ProjectProposalReviewItemResponse
            {
                ProjectId = p.ProjectId,
                TeamId = p.TeamId!.Value,
                TeamName = p.Team!.TeamName,
                ClassId = p.ClassId!.Value,
                ClassCode = p.Class!.ClassCode,
                Title = p.Title,
                LeaderStudentCode = p.Team!.Leader.StudentCode,
                SubmittedAt = p.BorrowRequests
                    .Where(br => br.Status == BorrowRequestStatuses.Pending)
                    .Max(br => br.RequestDate),
                RequestedComponentTypes = p.ProjectComponents.Count,
                TotalRequestedItems = p.ProjectComponents.Sum(pc => pc.Quantity),
            });

        return await PagedResult<ProjectProposalReviewItemResponse>.CreateAsync(proposals, paging, ct);
    }

    public async Task<ProjectProposalReviewDetailResponse> GetDetailAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var instructor = await GetCurrentInstructorAsync(ct);
        var project = await LoadReviewProjectAsync(projectId, ct);
        EnsureAssignedInstructor(project, instructor.InstructorProfileId);
        return ProjectProposalMappings.ToReviewDetailResponse(project);
    }

    public async Task<ProjectProposalReviewDetailResponse> ReviewAsync(
        Guid projectId, ReviewProjectProposalRequest request, CancellationToken ct = default)
    {
        var instructor = await GetCurrentInstructorAsync(ct);
        await using var transaction = await _uow.BeginTransactionAsync(ct);

        try
        {
            var project = await LoadReviewProjectAsync(projectId, ct);
            EnsureAssignedInstructor(project, instructor.InstructorProfileId);

            if (project.Status != ProjectStatuses.Submitted)
                throw new ConflictException("Đề tài không còn ở trạng thái Submitted");

            var borrowRequest = project.BorrowRequests
                                    .Where(br => br.Status == BorrowRequestStatuses.Pending)
                                    .OrderByDescending(br => br.RequestDate)
                                    .ThenByDescending(br => br.BorrowRequestId)
                                    .FirstOrDefault()
                                ?? throw new ConflictException("Không có yêu cầu mượn Pending để xét duyệt");

            var approved = request.Decision.Equals(
                ProjectStatuses.Approved, StringComparison.OrdinalIgnoreCase);
            var projectStatus = approved ? ProjectStatuses.Approved : ProjectStatuses.Rejected;
            var borrowRequestStatus = approved
                ? BorrowRequestStatuses.Approved
                : BorrowRequestStatuses.Rejected;
            var now = DateTime.UtcNow;
            DateTime? approvedAt = approved ? now : null;
            var rejectReason = approved ? null : request.RejectReason!.Trim();

            var requestUpdated = await _uow.Repository<BorrowRequest>().Query()
                .Where(br => br.BorrowRequestId == borrowRequest.BorrowRequestId &&
                             br.Status == BorrowRequestStatuses.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(br => br.Status, borrowRequestStatus)
                    .SetProperty(br => br.ApproverId, instructor.UserId)
                    .SetProperty(br => br.ApprovedAt, approvedAt)
                    .SetProperty(br => br.RejectReason, rejectReason), ct);
            if (requestUpdated != 1)
                throw new ConflictException("Yêu cầu đã được giảng viên khác xử lý");

            var projectUpdated = await _uow.Repository<Project>().Query()
                .Where(p => p.ProjectId == projectId && p.Status == ProjectStatuses.Submitted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Status, projectStatus)
                    .SetProperty(p => p.UpdatedAt, now), ct);
            if (projectUpdated != 1)
                throw new ConflictException("Đề tài đã được xử lý trước đó");

            if (!approved)
            {
                var reservedItemIds = borrowRequest.BorrowItems
                    .Where(bi => bi.Status == BorrowItemStatuses.Reserved && bi.ComponentItemId.HasValue)
                    .Select(bi => bi.ComponentItemId!.Value)
                    .Distinct()
                    .ToList();

                await _uow.Repository<BorrowItem>().Query()
                    .Where(bi => bi.BorrowRequestId == borrowRequest.BorrowRequestId &&
                                 bi.Status == BorrowItemStatuses.Reserved)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(bi => bi.Status, BorrowItemStatuses.Cancelled), ct);

                await _reservationService.ReleaseAsync(reservedItemIds, ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return await GetDetailAsync(projectId, ct);
    }

    private async Task<InstructorProfile> GetCurrentInstructorAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        return await _uow.Repository<InstructorProfile>().QueryNoTracking()
                   .Include(i => i.User)
                   .FirstOrDefaultAsync(i => i.UserId == userId && i.User.IsActive, ct)
               ?? throw new ForbiddenException("Tài khoản hiện tại không có hồ sơ giảng viên hoạt động");
    }

    private static void EnsureAssignedInstructor(Project project, Guid instructorProfileId)
    {
        if (project.Class is null || project.Class.InstructorId != instructorProfileId)
            throw new ForbiddenException("Bạn không phải giảng viên phụ trách lớp của đề tài này");
    }

    private async Task<Project> LoadReviewProjectAsync(Guid projectId, CancellationToken ct)
        => await _uow.Repository<Project>().QueryNoTracking()
               .Include(p => p.Class)
               .Include(p => p.Team).ThenInclude(t => t!.Leader).ThenInclude(s => s.User)
               .Include(p => p.ProjectComponents).ThenInclude(pc => pc.Component)
               .Include(p => p.BorrowRequests).ThenInclude(br => br.BorrowItems)
               .AsSplitQuery()
               .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
           ?? throw new NotFoundException("Không tìm thấy đề tài");
}

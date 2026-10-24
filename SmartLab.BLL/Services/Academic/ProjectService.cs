using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.DTOs.Borrowing;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.BLL.Services.Borrowing;
using SmartLab.BLL.Services.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class ProjectService : IProjectService
{
    private const string TeamProjectUniqueConstraint = "projects_team_id_key";
    private const string RequestCodeUniqueConstraint = "borrow_requests_request_code_key";
    private const string NotEditableMessage = "Project đang chờ duyệt hoặc đã được duyệt, không sửa được";
    private const string StatusChangedMessage = "Project vừa bị thay đổi bởi thao tác khác, vui lòng tải lại";

    /// <summary>Each round at least one of the racing requests is saved, so this covers that many simultaneous submits.</summary>
    private const int MaxCodeAttempts = 10;

    /// <summary>Vietnam has no DST, so a fixed UTC+7 offset is exact.</summary>
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IBorrowNotifier _notifier;
    private readonly IProjectNotifier _projectNotifier;

    public ProjectService(
        IUnitOfWork uow, ICurrentUserService currentUser, IBorrowNotifier notifier, IProjectNotifier projectNotifier)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifier = notifier;
        _projectNotifier = projectNotifier;
    }

    public Task<PagedResult<ProjectResponse>> GetProjectsAsync(
        ProjectQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var projects = VisibleProjects();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            projects = projects.Where(p => p.Title.ToLower().Contains(keyword)
                                        || (p.Team != null && p.Team.TeamName.ToLower().Contains(keyword)));
        }

        if (query.ClassId.HasValue)
            projects = projects.Where(p => p.ClassId == query.ClassId.Value);

        var status = ProjectStatuses.Normalize(query.Status);
        if (status != null)
            projects = projects.Where(p => p.Status == status);

        var result = projects
            .OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.ProjectId)
            .Select(p => new ProjectResponse
            {
                ProjectId = p.ProjectId,
                Title = p.Title,
                Status = p.Status,
                TeamId = p.TeamId,
                TeamName = p.Team != null ? p.Team.TeamName : null,
                ClassId = p.ClassId,
                ClassCode = p.Class != null ? p.Class.ClassCode : null,
                OwnerId = p.OwnerId,
                OwnerName = p.Owner.FullName,
                ComponentCount = p.ProjectComponents.Count,
                SubmittedAt = p.SubmittedAt,
                ReviewedAt = p.ReviewedAt,
                ReviewComment = p.ReviewComment,
                FinalStatus = p.FinalStatus,
                FinalSubmittedAt = p.FinalSubmittedAt,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            });

        return PagedResult<ProjectResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<ProjectDetailResponse> GetProjectByIdAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!await _uow.Repository<Project>().QueryNoTracking().AnyAsync(p => p.ProjectId == projectId, ct))
            throw new NotFoundException("Không tìm thấy project");

        return await VisibleProjects()
                   .Where(p => p.ProjectId == projectId)
                   .Select(p => new ProjectDetailResponse
                   {
                       ProjectId = p.ProjectId,
                       Title = p.Title,
                       Status = p.Status,
                       TeamId = p.TeamId,
                       TeamName = p.Team != null ? p.Team.TeamName : null,
                       ClassId = p.ClassId,
                       ClassCode = p.Class != null ? p.Class.ClassCode : null,
                       OwnerId = p.OwnerId,
                       OwnerName = p.Owner.FullName,
                       ComponentCount = p.ProjectComponents.Count,
                       SubmittedAt = p.SubmittedAt,
                       ReviewedAt = p.ReviewedAt,
                       CreatedAt = p.CreatedAt,
                       UpdatedAt = p.UpdatedAt,
                       Description = p.Description,
                       GithubUrl = p.GithubUrl,
                       SlideUrl = p.SlideUrl,
                       DemoVideoUrl = p.DemoVideoUrl,
                       ReviewedBy = p.ReviewedBy,
                       ReviewedByName = p.ReviewedByNavigation != null ? p.ReviewedByNavigation.FullName : null,
                       ReviewComment = p.ReviewComment,
                       FinalStatus = p.FinalStatus,
                       FinalSubmittedAt = p.FinalSubmittedAt,
                       FinalReviewedBy = p.FinalReviewedBy,
                       FinalReviewedByName = p.FinalReviewedByNavigation != null ? p.FinalReviewedByNavigation.FullName : null,
                       FinalReviewedAt = p.FinalReviewedAt,
                       FinalComment = p.FinalComment,
                       Components =p.ProjectComponents
                           .OrderBy(pc => pc.Component.ComponentCode)
                           .Select(pc => new ProjectComponentResponse
                           {
                               ComponentId = pc.ComponentId,
                               ComponentCode = pc.Component.ComponentCode,
                               ComponentName = pc.Component.ComponentName,
                               TrackingType = pc.Component.TrackingType,
                               Unit = pc.Component.Unit,
                               Quantity = pc.Quantity,
                               Note = pc.Note,
                           })
                           .ToList(),
                       SelfSuppliedItems = p.ProjectSelfSuppliedItems
                           .OrderBy(i => i.ItemName)
                           .Select(i => new SelfSuppliedItemResponse
                           {
                               ItemId = i.ItemId,
                               ItemName = i.ItemName,
                               Quantity = i.Quantity,
                               Note = i.Note,
                           })
                           .ToList(),
                       BorrowRequests = p.BorrowRequests
                           .OrderByDescending(r => r.RequestDate).ThenBy(r => r.BorrowRequestId)
                           .Select(r => new BorrowRequestSummaryResponse
                           {
                               BorrowRequestId = r.BorrowRequestId,
                               RequestCode = r.RequestCode,
                               Status = r.Status,
                               Purpose = r.Purpose,
                               RoomId = r.RoomId,
                               RoomCode = r.Room.RoomCode,
                               RoomName = r.Room.RoomName,
                               RequestDate = r.RequestDate,
                               BorrowDate = r.BorrowDate,
                               DueDate = r.DueDate,
                               RejectReason = r.RejectReason,
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
                           })
                           .ToList(),
                   })
                   .AsSplitQuery()
                   .FirstOrDefaultAsync(ct)
               ?? throw new ForbiddenException("Bạn không có quyền xem project này");
    }

    public async Task<ProjectDetailResponse> CreateProjectAsync(
        Guid teamId, CreateProjectRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var studentId = await GetCurrentStudentIdAsync(userId, ct);

        var team = await _uow.Repository<Team>().QueryNoTracking()
                       .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
                   ?? throw new NotFoundException("Không tìm thấy nhóm");
        if (team.LeaderId != studentId)
            throw new ForbiddenException("Chỉ nhóm trưởng được tạo project");
        await EnsureStudyingAsync(team.ClassId, studentId, ct);

        if (await _uow.Repository<Project>().QueryNoTracking().AnyAsync(p => p.TeamId == teamId, ct))
            throw new ConflictException("Nhóm đã có project");

        await EnsureComponentsUsableAsync(request.Components, ct);

        var now = DateTime.UtcNow;
        var project = new Project
        {
            Title = request.Title.Trim(),
            Description = CleanText(request.Description),
            OwnerId = userId,
            ClassId = team.ClassId,
            TeamId = teamId,
            Status = ProjectStatuses.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };
        foreach (var line in request.Components)
            project.ProjectComponents.Add(ToProjectComponent(line));
        foreach (var item in request.SelfSuppliedItems)
            project.ProjectSelfSuppliedItems.Add(ToSelfSuppliedItem(item, now));

        try
        {
            await _uow.Repository<Project>().AddAsync(project, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           {
                                               SqlState: PostgresErrorCodes.UniqueViolation,
                                               ConstraintName: TeamProjectUniqueConstraint,
                                           })
        {
            throw new ConflictException("Nhóm đã có project");
        }

        return await GetProjectByIdAsync(project.ProjectId, ct);
    }

    public async Task<ProjectDetailResponse> UpdateProjectAsync(
        Guid projectId, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await GetProjectForLeaderAsync(projectId, ct);
        if (!ProjectStatuses.Editable.Contains(project.Status))
            throw new ConflictException(NotEditableMessage);

        var components = request.Components!;
        await EnsureComponentsUsableAsync(components, ct);

        var title = request.Title.Trim();
        var description = CleanText(request.Description);
        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        var updated = await _uow.Repository<Project>().Query()
            .Where(p => p.ProjectId == projectId && ProjectStatuses.Editable.Contains(p.Status))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Title, title)
                .SetProperty(p => p.Description, description)
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (updated == 0)
            throw new ConflictException(NotEditableMessage);

        await _uow.Repository<ProjectComponent>().Query()
            .Where(pc => pc.ProjectId == projectId)
            .ExecuteDeleteAsync(ct);

        var projectComponents = _uow.Repository<ProjectComponent>();
        foreach (var line in components)
        {
            var projectComponent = ToProjectComponent(line);
            projectComponent.ProjectId = projectId;
            await projectComponents.AddAsync(projectComponent, ct);
        }

        await _uow.Repository<ProjectSelfSuppliedItem>().Query()
            .Where(i => i.ProjectId == projectId)
            .ExecuteDeleteAsync(ct);

        var selfSuppliedItems = _uow.Repository<ProjectSelfSuppliedItem>();
        foreach (var item in request.SelfSuppliedItems!)
        {
            var selfSuppliedItem = ToSelfSuppliedItem(item, now);
            selfSuppliedItem.ProjectId = projectId;
            await selfSuppliedItems.AddAsync(selfSuppliedItem, ct);
        }

        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetProjectByIdAsync(projectId, ct);
    }

    public async Task<ProjectDetailResponse> UpdateProjectLinksAsync(
        Guid projectId, UpdateProjectLinksRequest request, CancellationToken ct = default)
    {
        await GetProjectForLeaderAsync(projectId, ct);

        var project = await _uow.Repository<Project>().Query()
                          .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
                      ?? throw new NotFoundException("Không tìm thấy project");
        if (project.FinalStatus != null && FinalSubmissionStatuses.Locked.Contains(project.FinalStatus))
            throw new ConflictException("Bài nộp đang chờ hoặc đã được xác nhận, không sửa link được");

        project.GithubUrl = CleanText(request.GithubUrl);
        project.SlideUrl = CleanText(request.SlideUrl);
        project.DemoVideoUrl = CleanText(request.DemoVideoUrl);
        project.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return await GetProjectByIdAsync(projectId, ct);
    }

    public async Task<ProjectDetailResponse> SubmitProjectAsync(
        Guid projectId, SubmitProjectRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var project = await GetProjectForLeaderAsync(projectId, ct);
        if (!ProjectStatuses.Editable.Contains(project.Status))
            throw new ConflictException("Project đã được gửi duyệt hoặc đã được duyệt");

        var now = DateTime.UtcNow;
        var borrowDate = ToVietnamDateUtc(request.BorrowDate!.Value);
        var dueDate = ToVietnamDateUtc(request.DueDate!.Value);
        if (borrowDate < ToVietnamDateUtc(now))
            throw new BadRequestException("Ngày mượn không được trước hôm nay");

        var maxBorrowDays = await SystemSettingValues.GetPositiveIntAsync(
            _uow, SystemSettingKeys.BorrowMaxDays, "số ngày mượn tối đa", ct);
        if (dueDate > borrowDate.AddDays(maxBorrowDays))
            throw new BadRequestException($"Thời gian mượn tối đa {maxBorrowDays} ngày");

        var pickupRoomId = await PickupRoom.GetActiveIdAsync(_uow, ct);

        var lecturerId = await _uow.Repository<Class>().QueryNoTracking()
            .Where(c => c.ClassId == project.ClassId && c.Instructor != null)
            .Select(c => (Guid?)c.Instructor!.UserId)
            .FirstOrDefaultAsync(ct);
        if (lecturerId == null)
            throw new BadRequestException("Lớp chưa có giảng viên phụ trách nên chưa gửi duyệt được");

        BorrowRequest? borrowRequest = null;
        var attempt = 1;
        while (true)
        {
            await using var tx = await _uow.BeginTransactionAsync(ct);

            var updated = await _uow.Repository<Project>().Query()
                .Where(p => p.ProjectId == projectId && ProjectStatuses.Editable.Contains(p.Status))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, ProjectStatuses.Submitted)
                    .SetProperty(p => p.SubmittedAt, now)
                    .SetProperty(p => p.ReviewedBy, (Guid?)null)
                    .SetProperty(p => p.ReviewedAt, (DateTime?)null)
                    .SetProperty(p => p.ReviewComment, (string?)null)
                    .SetProperty(p => p.UpdatedAt, now), ct);
            if (updated == 0)
                throw new ConflictException(StatusChangedMessage);

            if (borrowRequest == null)
            {
                var lines = await LoadSubmitLinesAsync(projectId, ct);
                await EnsureStockAsync(lines, ct);

                borrowRequest = new BorrowRequest
                {
                    RequestCode = await GenerateRequestCodeAsync(now, ct),
                    RequesterId = userId,
                    ClassId = project.ClassId,
                    ProjectId = projectId,
                    Purpose = $"Mượn linh kiện cho project: {project.Title}",
                    Status = BorrowRequestStatuses.Pending,
                    RequestDate = now,
                    BorrowDate = borrowDate,
                    DueDate = dueDate,
                    RoomId = pickupRoomId,
                };
                foreach (var line in lines)
                {
                    borrowRequest.BorrowItems.Add(new BorrowItem
                    {
                        ComponentId = line.ComponentId,
                        Quantity = line.Quantity,
                        Status = BorrowItemStatuses.Pending,
                    });
                }
                borrowRequest.BorrowApprovals.Add(new BorrowApproval
                {
                    StepNo = 1,
                    ApproverRole = ApproverRoles.Lecturer,
                    ApproverId = lecturerId.Value,
                    Status = BorrowApprovalStatuses.Pending,
                    CreatedAt = now,
                });

                await _uow.Repository<BorrowRequest>().AddAsync(borrowRequest, ct);
            }

            try
            {
                await _uow.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                               {
                                                   SqlState: PostgresErrorCodes.UniqueViolation,
                                                   ConstraintName: RequestCodeUniqueConstraint,
                                               } && attempt < MaxCodeAttempts)
            {
                await tx.RollbackAsync(ct);
                attempt++;
                borrowRequest.RequestCode = await GenerateRequestCodeAsync(now, ct);
            }
        }

        await _notifier.SubmittedAsync(borrowRequest.BorrowRequestId, ct);

        return await GetProjectByIdAsync(projectId, ct);
    }

    public async Task<ProjectDetailResponse> SubmitSupplementaryBorrowAsync(
        Guid projectId, SupplementaryBorrowRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var project = await GetProjectForLeaderAsync(projectId, ct);
        if (project.Status != ProjectStatuses.Approved)
            throw new ConflictException("Chỉ mượn thêm được khi project đã được duyệt");
        if (project.FinalStatus != null && FinalSubmissionStatuses.Locked.Contains(project.FinalStatus))
            throw new ConflictException("Đã nộp bài cuối, không mượn thêm được");

        var now = DateTime.UtcNow;
        var borrowDate = ToVietnamDateUtc(request.BorrowDate!.Value);
        var dueDate = ToVietnamDateUtc(request.DueDate!.Value);
        if (borrowDate < ToVietnamDateUtc(now))
            throw new BadRequestException("Ngày mượn không được trước hôm nay");

        var maxBorrowDays = await SystemSettingValues.GetPositiveIntAsync(
            _uow, SystemSettingKeys.BorrowMaxDays, "số ngày mượn tối đa", ct);
        if (dueDate > borrowDate.AddDays(maxBorrowDays))
            throw new BadRequestException($"Thời gian mượn tối đa {maxBorrowDays} ngày");

        await EnsureComponentsUsableAsync(request.Components, ct);
        var lines = await LoadRequestLinesAsync(request.Components, ct);

        var pickupRoomId = await PickupRoom.GetActiveIdAsync(_uow, ct);

        var lecturerId = await _uow.Repository<Class>().QueryNoTracking()
            .Where(c => c.ClassId == project.ClassId && c.Instructor != null)
            .Select(c => (Guid?)c.Instructor!.UserId)
            .FirstOrDefaultAsync(ct);
        if (lecturerId == null)
            throw new BadRequestException("Lớp chưa có giảng viên phụ trách nên chưa gửi duyệt được");

        var purpose = $"Mượn bổ sung cho project: {project.Title}. Lý do: {request.Reason.Trim()}";

        BorrowRequest? borrowRequest = null;
        var attempt = 1;
        while (true)
        {
            await using var tx = await _uow.BeginTransactionAsync(ct);

            var locked = await _uow.Repository<Project>().Query()
                .Where(p => p.ProjectId == projectId
                            && p.Status == ProjectStatuses.Approved
                            && (p.FinalStatus == null || !FinalSubmissionStatuses.Locked.Contains(p.FinalStatus)))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdatedAt, now), ct);
            if (locked == 0)
                throw new ConflictException(StatusChangedMessage);

            if (borrowRequest == null)
            {
                var hasPending = await _uow.Repository<BorrowRequest>().QueryNoTracking()
                    .AnyAsync(r => r.ProjectId == projectId && r.Status == BorrowRequestStatuses.Pending, ct);
                if (hasPending)
                    throw new ConflictException("Nhóm đang có đơn mượn thêm chờ duyệt");

                await EnsureStockAsync(lines, ct);

                borrowRequest = new BorrowRequest
                {
                    RequestCode = await GenerateRequestCodeAsync(now, ct),
                    RequesterId = userId,
                    ClassId = project.ClassId,
                    ProjectId = projectId,
                    Purpose = purpose,
                    Status = BorrowRequestStatuses.Pending,
                    RequestDate = now,
                    BorrowDate = borrowDate,
                    DueDate = dueDate,
                    RoomId = pickupRoomId,
                };
                foreach (var line in lines)
                {
                    borrowRequest.BorrowItems.Add(new BorrowItem
                    {
                        ComponentId = line.ComponentId,
                        Quantity = line.Quantity,
                        Status = BorrowItemStatuses.Pending,
                    });
                }
                borrowRequest.BorrowApprovals.Add(new BorrowApproval
                {
                    StepNo = 1,
                    ApproverRole = ApproverRoles.Lecturer,
                    ApproverId = lecturerId.Value,
                    Status = BorrowApprovalStatuses.Pending,
                    CreatedAt = now,
                });

                await _uow.Repository<BorrowRequest>().AddAsync(borrowRequest, ct);
            }

            try
            {
                await _uow.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                               {
                                                   SqlState: PostgresErrorCodes.UniqueViolation,
                                                   ConstraintName: RequestCodeUniqueConstraint,
                                               } && attempt < MaxCodeAttempts)
            {
                await tx.RollbackAsync(ct);
                attempt++;
                borrowRequest.RequestCode = await GenerateRequestCodeAsync(now, ct);
            }
        }

        await _notifier.SubmittedAsync(borrowRequest.BorrowRequestId, ct);

        return await GetProjectByIdAsync(projectId, ct);
    }

    public async Task<ProjectDetailResponse> WithdrawSupplementaryBorrowAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await GetProjectForLeaderAsync(projectId, ct);
        if (project.Status != ProjectStatuses.Approved)
            throw new ConflictException("Project không có đơn mượn thêm đang chờ duyệt");

        var cancelled = await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.ProjectId == projectId
                        && r.Status == BorrowRequestStatuses.Pending
                        && r.Project!.Status == ProjectStatuses.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, BorrowRequestStatuses.Cancelled), ct);
        if (cancelled == 0)
            throw new ConflictException("Không có đơn mượn thêm đang chờ duyệt hoặc giảng viên vừa xử lý xong");

        return await GetProjectByIdAsync(projectId, ct);
    }

    public async Task<ProjectDetailResponse> SubmitFinalAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await GetProjectForLeaderAsync(projectId, ct);
        if (project.Status != ProjectStatuses.Approved)
            throw new ConflictException("Chỉ nộp bài được khi project đã được duyệt");
        if (project.GithubUrl == null || project.SlideUrl == null || project.DemoVideoUrl == null)
            throw new BadRequestException("Cần nhập đủ link GitHub, slide và video demo trước khi nộp");
        if (project.FinalStatus != null && project.FinalStatus != FinalSubmissionStatuses.Returned)
            throw new ConflictException("Bài nộp đang chờ hoặc đã được xác nhận");

        var now = DateTime.UtcNow;
        var updated = await _uow.Repository<Project>().Query()
            .Where(p => p.ProjectId == projectId
                        && p.Status == ProjectStatuses.Approved
                        && p.GithubUrl != null && p.SlideUrl != null && p.DemoVideoUrl != null
                        && (p.FinalStatus == null || p.FinalStatus == FinalSubmissionStatuses.Returned))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.FinalStatus, FinalSubmissionStatuses.Pending)
                .SetProperty(p => p.FinalSubmittedAt, now)
                .SetProperty(p => p.FinalReviewedBy, (Guid?)null)
                .SetProperty(p => p.FinalReviewedAt, (DateTime?)null)
                .SetProperty(p => p.FinalComment, (string?)null)
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (updated == 0)
            throw new ConflictException(StatusChangedMessage);

        await _projectNotifier.FinalSubmittedAsync(projectId, ct);

        return await GetProjectByIdAsync(projectId, ct);
    }

    public Task<PagedResult<PendingFinalSubmissionResponse>> GetPendingFinalSubmissionsAsync(
        FinalSubmissionQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var projects = _uow.Repository<Project>().QueryNoTracking()
            .Where(p => p.FinalStatus == FinalSubmissionStatuses.Pending);

        if (!_currentUser.IsInRole(RoleNames.Admin))
            projects = projects.Where(p => p.Class != null && p.Class.Instructor != null && p.Class.Instructor.UserId == userId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            projects = projects.Where(p => p.Title.ToLower().Contains(keyword)
                                        || (p.Team != null && p.Team.TeamName.ToLower().Contains(keyword)));
        }

        var result = projects
            .OrderBy(p => p.FinalSubmittedAt).ThenBy(p => p.ProjectId)
            .Select(p => new PendingFinalSubmissionResponse
            {
                ProjectId = p.ProjectId,
                Title = p.Title,
                TeamName = p.Team != null ? p.Team.TeamName : null,
                ClassCode = p.Class != null ? p.Class.ClassCode : null,
                LeaderName = p.Team != null ? p.Team.Leader.User.FullName : null,
                GithubUrl = p.GithubUrl,
                SlideUrl = p.SlideUrl,
                DemoVideoUrl = p.DemoVideoUrl,
                FinalSubmittedAt = p.FinalSubmittedAt,
            });

        return PagedResult<PendingFinalSubmissionResponse>.CreateAsync(result, paging, ct);
    }

    public Task<ProjectDetailResponse> ApproveFinalAsync(
        Guid projectId, ApproveFinalSubmissionRequest request, CancellationToken ct = default)
        => ReviewFinalAsync(projectId, FinalSubmissionStatuses.Accepted, CleanText(request.Comment), ct);

    public Task<ProjectDetailResponse> RejectFinalAsync(
        Guid projectId, RejectFinalSubmissionRequest request, CancellationToken ct = default)
        => ReviewFinalAsync(projectId, FinalSubmissionStatuses.Returned, CleanText(request.Comment), ct);

    public async Task<ProjectDetailResponse> WithdrawProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await GetProjectForLeaderAsync(projectId, ct);
        if (project.Status != ProjectStatuses.Submitted)
            throw new ConflictException("Chỉ rút lại được project đang chờ duyệt");

        var now = DateTime.UtcNow;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        var cancelled = await _uow.Repository<BorrowRequest>().Query()
            .Where(r => r.ProjectId == projectId && r.Status == BorrowRequestStatuses.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, BorrowRequestStatuses.Cancelled), ct);
        if (cancelled == 0)
            throw new ConflictException(StatusChangedMessage);

        var updated = await _uow.Repository<Project>().Query()
            .Where(p => p.ProjectId == projectId && p.Status == ProjectStatuses.Submitted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, ProjectStatuses.Draft)
                .SetProperty(p => p.SubmittedAt, (DateTime?)null)
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (updated == 0)
            throw new ConflictException(StatusChangedMessage);

        await tx.CommitAsync(ct);

        return await GetProjectByIdAsync(projectId, ct);
    }

    /// <summary>
    /// Admin sees every project; others see their team's, their classes', and the ones they approve.
    /// </summary>
    private IQueryable<Project> VisibleProjects()
    {
        var projects = _uow.Repository<Project>().QueryNoTracking();
        if (_currentUser.IsInRole(RoleNames.Admin))
            return projects;

        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        return projects.Where(p =>
            (p.Team != null && (p.Team.Leader.UserId == userId
                                || p.Team.TeamMembers.Any(tm => tm.Student.UserId == userId
                                                                && tm.Status == TeamMemberStatuses.Accepted)))
            || (p.Class != null && p.Class.Instructor != null && p.Class.Instructor.UserId == userId)
            || p.BorrowRequests.Any(r => r.BorrowApprovals.Any(a => a.ApproverId == userId)));
    }

    /// <summary>
    /// The lecturer of the project's class (or an Admin standing in) decides a Pending final submission. The UPDATE only
    /// matches while it is still Pending, so when two reviewers race only the first one wins.
    /// </summary>
    private async Task<ProjectDetailResponse> ReviewFinalAsync(
        Guid projectId, string newStatus, string? comment, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var project = await _uow.Repository<Project>().QueryNoTracking()
                          .Where(p => p.ProjectId == projectId)
                          .Select(p => new
                          {
                              p.FinalStatus,
                              LecturerUserId = p.Class != null && p.Class.Instructor != null
                                  ? (Guid?)p.Class.Instructor.UserId
                                  : null,
                          })
                          .FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Không tìm thấy project");

        if (project.LecturerUserId != userId && !_currentUser.IsInRole(RoleNames.Admin))
            throw new ForbiddenException("Chỉ giảng viên của lớp được xác nhận bài nộp này");
        if (project.FinalStatus != FinalSubmissionStatuses.Pending)
            throw new ConflictException("Bài nộp đã được xử lý hoặc chưa được nộp");

        var now = DateTime.UtcNow;
        var updated = await _uow.Repository<Project>().Query()
            .Where(p => p.ProjectId == projectId && p.FinalStatus == FinalSubmissionStatuses.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.FinalStatus, newStatus)
                .SetProperty(p => p.FinalReviewedBy, userId)
                .SetProperty(p => p.FinalReviewedAt, now)
                .SetProperty(p => p.FinalComment, comment)
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (updated == 0)
            throw new ConflictException("Bài nộp đã được xử lý");

        await _projectNotifier.FinalReviewedAsync(projectId, ct);

        return await GetProjectByIdAsync(projectId, ct);
    }

    private async Task<Guid> GetCurrentStudentIdAsync(Guid userId, CancellationToken ct)
    {
        var studentId = await _uow.Repository<StudentProfile>().QueryNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => (Guid?)s.StudentProfileId)
            .FirstOrDefaultAsync(ct);
        if (studentId == null)
            throw new ForbiddenException("Chỉ sinh viên (nhóm trưởng) được thao tác với project");

        return studentId.Value;
    }

    /// <summary>The project, after checking that the current user leads its team and still studies in the class.</summary>
    private async Task<Project> GetProjectForLeaderAsync(Guid projectId, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var project = await _uow.Repository<Project>().QueryNoTracking()
                          .Include(p => p.Team)
                          .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
                      ?? throw new NotFoundException("Không tìm thấy project");

        var studentId = await GetCurrentStudentIdAsync(userId, ct);
        if (project.Team == null || project.Team.LeaderId != studentId)
            throw new ForbiddenException("Chỉ nhóm trưởng được sửa hoặc gửi duyệt project");
        await EnsureStudyingAsync(project.Team.ClassId, studentId, ct);

        return project;
    }

    private async Task EnsureStudyingAsync(Guid classId, Guid studentId, CancellationToken ct)
    {
        var studying = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .AnyAsync(cs => cs.ClassId == classId && cs.StudentId == studentId
                                                  && cs.Status == ClassStudentStatuses.Studying, ct);
        if (!studying)
            throw new ForbiddenException("Bạn không còn học lớp của nhóm này");
    }

    private async Task EnsureComponentsUsableAsync(List<ProjectComponentRequest> lines, CancellationToken ct)
    {
        if (lines.Count == 0)
            return;

        var ids = lines.Select(l => l.ComponentId).ToList();
        var components = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => ids.Contains(c.ComponentId))
            .Select(c => new { c.ComponentId, c.ComponentName, c.IsActive })
            .ToListAsync(ct);

        if (components.Count != ids.Count)
            throw new NotFoundException("Không tìm thấy linh kiện");

        var inactive = components.Where(c => !c.IsActive).Select(c => c.ComponentName).ToList();
        if (inactive.Count > 0)
            throw new BadRequestException($"Linh kiện đã ngừng sử dụng: {string.Join(", ", inactive)}");
    }

    private sealed record SubmitLine(Guid ComponentId, string ComponentName, bool IsActive, int Quantity);

    /// <summary>Reads the saved components after the project row is locked, and checks the project can be sent.</summary>
    private async Task<List<SubmitLine>> LoadSubmitLinesAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _uow.Repository<Project>().QueryNoTracking()
            .Where(p => p.ProjectId == projectId)
            .Select(p => new { p.Description })
            .FirstAsync(ct);
        if (string.IsNullOrWhiteSpace(project.Description))
            throw new BadRequestException("Project phải có mô tả trước khi gửi duyệt");

        var lines = await _uow.Repository<ProjectComponent>().QueryNoTracking()
            .Where(pc => pc.ProjectId == projectId)
            .OrderBy(pc => pc.Component.ComponentCode)
            .Select(pc => new SubmitLine(pc.ComponentId, pc.Component.ComponentName,
                pc.Component.IsActive, pc.Quantity))
            .ToListAsync(ct);
        if (lines.Count == 0)
            throw new BadRequestException("Project chưa có linh kiện nào, không gửi duyệt được");

        var inactive = lines.Where(l => !l.IsActive).Select(l => l.ComponentName).ToList();
        if (inactive.Count > 0)
            throw new BadRequestException($"Linh kiện đã ngừng sử dụng: {string.Join(", ", inactive)}");

        return lines;
    }

    /// <summary>The lines of a supplementary request, with component names for the stock message.</summary>
    private async Task<List<SubmitLine>> LoadRequestLinesAsync(List<ProjectComponentRequest> requested, CancellationToken ct)
    {
        var ids = requested.Select(l => l.ComponentId).ToList();
        var components = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => ids.Contains(c.ComponentId))
            .Select(c => new { c.ComponentId, c.ComponentCode, c.ComponentName, c.IsActive })
            .ToDictionaryAsync(c => c.ComponentId, ct);

        return requested
            .Select(l => new SubmitLine(l.ComponentId, components[l.ComponentId].ComponentName,
                components[l.ComponentId].IsActive, l.Quantity))
            .OrderBy(l => components[l.ComponentId].ComponentCode)
            .ToList();
    }

    /// <summary>
    /// All labs together must be able to lend each component now: their stock minus what Approved / Ready requests
    /// already reserve (see <see cref="RoomStock"/>). Which lab gives what is decided when the lecturer approves.
    /// </summary>
    private async Task EnsureStockAsync(List<SubmitLine> lines, CancellationToken ct)
    {
        var ids = lines.Select(l => l.ComponentId).ToList();
        var available = await RoomStock.GetTotalAvailableAsync(_uow, ids, ct);

        var shortages = new List<string>();
        foreach (var line in lines)
        {
            var left = available.GetValueOrDefault(line.ComponentId);
            if (left < line.Quantity)
                shortages.Add($"{line.ComponentName} (cần {line.Quantity}, còn {left})");
        }

        if (shortages.Count > 0)
            throw new BadRequestException($"Kho không đủ linh kiện: {string.Join("; ", shortages)}");
    }

    /// <summary>PM-yyyyMMdd-nnn by Vietnam date: highest number of the day + 1.</summary>
    private async Task<string> GenerateRequestCodeAsync(DateTime now, CancellationToken ct)
    {
        var prefix = $"PM-{now.Add(VietnamUtcOffset):yyyyMMdd}-";
        var todayCodes = await _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.RequestCode.StartsWith(prefix))
            .Select(r => r.RequestCode)
            .ToListAsync(ct);

        var maxNumber = 0;
        foreach (var code in todayCodes)
        {
            if (int.TryParse(code.Substring(prefix.Length), out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"{prefix}{maxNumber + 1:D3}";
    }

    /// <summary>
    /// Start of the Vietnam calendar day, in UTC. A plain date ("2026-10-28") is that day. A value with "Z" or an offset
    /// (and DateTime.UtcNow) is first turned into Vietnam time, so the server time zone never shifts the day.
    /// </summary>
    private static DateTime ToVietnamDateUtc(DateTime vietnamDate)
    {
        var date = vietnamDate.Date;
        if (vietnamDate.Kind != DateTimeKind.Unspecified)
            date = (vietnamDate.ToUniversalTime() + VietnamUtcOffset).Date;

        return DateTime.SpecifyKind(date - VietnamUtcOffset, DateTimeKind.Utc);
    }

    private static ProjectComponent ToProjectComponent(ProjectComponentRequest line) => new()
    {
        ComponentId = line.ComponentId,
        Quantity = line.Quantity,
        Note = CleanText(line.Note),
    };

    private static ProjectSelfSuppliedItem ToSelfSuppliedItem(SelfSuppliedItemRequest item, DateTime now) => new()
    {
        ItemName = item.ItemName.Trim(),
        Quantity = item.Quantity,
        Note = CleanText(item.Note),
        CreatedAt = now,
    };

    private static string? CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Trim();
    }
}

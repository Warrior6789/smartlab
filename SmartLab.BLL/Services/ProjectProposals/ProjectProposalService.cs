using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ProjectProposals;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.ProjectProposals;
using SmartLab.BLL.Mappings.ProjectProposals;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.ProjectProposals;

public class ProjectProposalService : IProjectProposalService
{
    private const string StudyingStatus = "Studying";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IComponentReservationService _reservationService;

    public ProjectProposalService(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IComponentReservationService reservationService)
    {
        _uow = uow;
        _currentUser = currentUser;
        _reservationService = reservationService;
    }

    public async Task<ProjectProposalResponse> SubmitAsync(
        Guid teamId, SubmitProjectProposalRequest request, CancellationToken ct = default)
    {
        var student = await GetCurrentStudentAsync(ct);
        var team = await LoadTeamForSubmissionAsync(teamId, ct);
        await EnsureTeamCanSubmitAsync(team, student, ct);

        var dueDate = GetDueDate(team.Class.Semester.EndDate);
        var now = DateTime.UtcNow;
        await using var transaction = await _uow.BeginTransactionAsync(ct);

        try
        {
            var teamLocked = await _uow.Repository<Team>().Query()
                .Where(t => t.TeamId == teamId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.UpdatedAt, now), ct);
            if (teamLocked != 1)
                throw new NotFoundException("Không tìm thấy nhóm");

            if (await _uow.Repository<Project>().QueryNoTracking().AnyAsync(p => p.TeamId == teamId, ct))
                throw new ConflictException("Nhóm đã có đề tài; đề tài bị từ chối phải được gửi lại bằng chức năng resubmit");

            var reservations = await _reservationService.ReserveAsync(request.Components, ct);
            var project = BuildProject(team, student.UserId, request, reservations, dueDate, now);

            await _uow.Repository<Project>().AddAsync(project, ct);
            await _uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ProjectProposalMappings.ToResponse(await LoadProjectAsync(project.ProjectId, ct));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(ct);
            throw new ConflictException("Mã yêu cầu mượn hoặc dữ liệu đề tài bị trùng");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ProjectProposalResponse> GetByTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var student = await GetCurrentStudentAsync(ct);
        var team = await _uow.Repository<Team>().QueryNoTracking()
                       .Include(t => t.TeamMembers)
                       .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
                   ?? throw new NotFoundException("Không tìm thấy nhóm");

        var canView = team.LeaderId == student.StudentProfileId ||
                      team.TeamMembers.Any(tm => tm.StudentId == student.StudentProfileId &&
                                                 tm.Status == TeamMemberStatuses.Accepted);
        if (!canView)
            throw new ForbiddenException("Bạn không phải thành viên đã được chấp nhận của nhóm này");

        var projectIds = await _uow.Repository<Project>().QueryNoTracking()
            .Where(p => p.TeamId == teamId)
            .Select(p => p.ProjectId)
            .Take(2)
            .ToListAsync(ct);

        if (projectIds.Count == 0)
            throw new NotFoundException("Nhóm chưa có đề tài");
        if (projectIds.Count > 1)
            throw new ConflictException("Dữ liệu không nhất quán: nhóm có nhiều đề tài");

        return ProjectProposalMappings.ToResponse(await LoadProjectAsync(projectIds[0], ct));
    }

    public async Task<ProjectProposalResponse> ResubmitAsync(
        Guid projectId, SubmitProjectProposalRequest request, CancellationToken ct = default)
    {
        var student = await GetCurrentStudentAsync(ct);
        var project = await _uow.Repository<Project>().Query()
                          .Include(p => p.Team).ThenInclude(t => t!.Class).ThenInclude(c => c.Semester)
                          .Include(p => p.Team).ThenInclude(t => t!.TeamMembers)
                          .Include(p => p.ProjectComponents)
                          .Include(p => p.BorrowRequests)
                          .AsSplitQuery()
                          .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
                      ?? throw new NotFoundException("Không tìm thấy đề tài");

        if (project.Team is null)
            throw new ConflictException("Đề tài không còn liên kết với nhóm");

        await EnsureTeamCanSubmitAsync(project.Team, student, ct);

        if (project.Status != ProjectStatuses.Rejected)
            throw new ConflictException("Chỉ đề tài đã bị từ chối mới được gửi lại");

        var latestRequest = project.BorrowRequests
            .OrderByDescending(br => br.RequestDate)
            .ThenByDescending(br => br.BorrowRequestId)
            .FirstOrDefault();
        if (latestRequest is null || latestRequest.Status != BorrowRequestStatuses.Rejected)
            throw new ConflictException("Yêu cầu mượn gần nhất chưa ở trạng thái Rejected");

        var dueDate = GetDueDate(project.Team.Class.Semester.EndDate);
        var now = DateTime.UtcNow;
        await using var transaction = await _uow.BeginTransactionAsync(ct);

        try
        {
            var projectClaimed = await _uow.Repository<Project>().Query()
                .Where(p => p.ProjectId == projectId && p.Status == ProjectStatuses.Rejected)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Status, ProjectStatuses.Submitted)
                    .SetProperty(p => p.Title, request.Title.Trim())
                    .SetProperty(p => p.Description, request.Description.Trim())
                    .SetProperty(p => p.UpdatedAt, now), ct);
            if (projectClaimed != 1)
                throw new ConflictException("Đề tài đã được gửi lại hoặc thay đổi trạng thái trước đó");

            var reservations = await _reservationService.ReserveAsync(request.Components, ct);
            SynchronizeProjectComponents(project, request.Components);

            project.Title = request.Title.Trim();
            project.Description = request.Description.Trim();
            project.Status = ProjectStatuses.Submitted;
            project.UpdatedAt = now;

            var borrowRequest = BuildBorrowRequest(
                project.ProjectId,
                project.Team.ClassId,
                student.UserId,
                request.Description.Trim(),
                reservations,
                dueDate,
                now);
            await _uow.Repository<BorrowRequest>().AddAsync(borrowRequest, ct);

            await _uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ProjectProposalMappings.ToResponse(await LoadProjectAsync(project.ProjectId, ct));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(ct);
            throw new ConflictException("Mã yêu cầu mượn hoặc dữ liệu đề tài bị trùng");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private Project BuildProject(
        Team team,
        Guid ownerId,
        SubmitProjectProposalRequest request,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> reservations,
        DateTime dueDate,
        DateTime now)
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            OwnerId = ownerId,
            ClassId = team.ClassId,
            TeamId = team.TeamId,
            Status = ProjectStatuses.Submitted,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var component in request.Components)
        {
            project.ProjectComponents.Add(new ProjectComponent
            {
                ProjectId = projectId,
                ComponentId = component.ComponentId,
                Quantity = component.Quantity,
            });
        }

        project.BorrowRequests.Add(BuildBorrowRequest(
            projectId,
            team.ClassId,
            ownerId,
            request.Description.Trim(),
            reservations,
            dueDate,
            now));

        return project;
    }

    private static BorrowRequest BuildBorrowRequest(
        Guid projectId,
        Guid classId,
        Guid requesterId,
        string purpose,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> reservations,
        DateTime dueDate,
        DateTime now)
    {
        var borrowRequestId = Guid.NewGuid();
        var borrowRequest = new BorrowRequest
        {
            BorrowRequestId = borrowRequestId,
            RequestCode = GenerateRequestCode(now),
            RequesterId = requesterId,
            ClassId = classId,
            ProjectId = projectId,
            Purpose = purpose,
            Status = BorrowRequestStatuses.Pending,
            RequestDate = now,
            DueDate = dueDate,
        };

        foreach (var reservation in reservations)
        {
            foreach (var itemId in reservation.Value)
            {
                borrowRequest.BorrowItems.Add(new BorrowItem
                {
                    BorrowItemId = Guid.NewGuid(),
                    BorrowRequestId = borrowRequestId,
                    ComponentId = reservation.Key,
                    ComponentItemId = itemId,
                    Status = BorrowItemStatuses.Reserved,
                });
            }
        }

        return borrowRequest;
    }

    private void SynchronizeProjectComponents(
        Project project, IReadOnlyList<ProposalComponentRequest> requestedComponents)
    {
        var requestedById = requestedComponents.ToDictionary(c => c.ComponentId);
        var repository = _uow.Repository<ProjectComponent>();

        foreach (var existing in project.ProjectComponents.ToList())
        {
            if (requestedById.TryGetValue(existing.ComponentId, out var requested))
            {
                existing.Quantity = requested.Quantity;
                requestedById.Remove(existing.ComponentId);
            }
            else
            {
                repository.Remove(existing);
            }
        }

        foreach (var requested in requestedById.Values)
        {
            project.ProjectComponents.Add(new ProjectComponent
            {
                ProjectId = project.ProjectId,
                ComponentId = requested.ComponentId,
                Quantity = requested.Quantity,
            });
        }
    }

    private async Task EnsureTeamCanSubmitAsync(Team team, StudentProfile student, CancellationToken ct)
    {
        if (team.LeaderId != student.StudentProfileId)
            throw new ForbiddenException("Chỉ trưởng nhóm mới được gửi đề tài");

        var isEnrolled = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .AnyAsync(cs => cs.ClassId == team.ClassId &&
                            cs.StudentId == student.StudentProfileId &&
                            cs.Status == StudyingStatus, ct);
        if (!isEnrolled)
            throw new ForbiddenException("Trưởng nhóm không còn thuộc lớp ở trạng thái Studying");

        if (!team.Class.Semester.IsActive)
            throw new ConflictException("Học kỳ của lớp không còn hoạt động");
        if (!team.Class.InstructorId.HasValue)
            throw new ConflictException("Lớp chưa được phân công giảng viên");
        if (team.TeamMembers.Any(tm => tm.Status == TeamMemberStatuses.Pending))
            throw new ConflictException("Nhóm vẫn còn lời mời đang chờ phản hồi");
    }

    private async Task<StudentProfile> GetCurrentStudentAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        return await _uow.Repository<StudentProfile>().QueryNoTracking()
                   .Include(s => s.User)
                   .FirstOrDefaultAsync(s => s.UserId == userId && s.User.IsActive, ct)
               ?? throw new ForbiddenException("Tài khoản hiện tại không có hồ sơ sinh viên hoạt động");
    }

    private async Task<Team> LoadTeamForSubmissionAsync(Guid teamId, CancellationToken ct)
        => await _uow.Repository<Team>().QueryNoTracking()
               .Include(t => t.Class).ThenInclude(c => c.Semester)
               .Include(t => t.TeamMembers)
               .AsSplitQuery()
               .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
           ?? throw new NotFoundException("Không tìm thấy nhóm");

    private async Task<Project> LoadProjectAsync(Guid projectId, CancellationToken ct)
        => await _uow.Repository<Project>().QueryNoTracking()
               .Include(p => p.ProjectComponents).ThenInclude(pc => pc.Component)
               .Include(p => p.BorrowRequests).ThenInclude(br => br.BorrowItems)
               .AsSplitQuery()
               .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
           ?? throw new NotFoundException("Không tìm thấy đề tài");

    private static DateTime GetDueDate(DateOnly semesterEndDate)
    {
        var dueDate = DateTime.SpecifyKind(
            semesterEndDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
        if (dueDate <= DateTime.UtcNow)
            throw new ConflictException("Ngày kết thúc học kỳ đã qua, không thể tạo yêu cầu mượn");
        return dueDate;
    }

    private static string GenerateRequestCode(DateTime now)
        => $"BR-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}

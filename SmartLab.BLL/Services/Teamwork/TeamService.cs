using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Teamwork;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Teamwork;
using SmartLab.BLL.Mappings.Teamwork;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Teamwork;

public class TeamService : ITeamService
{
    private const string StudyingStatus = "Studying";

    private static readonly string[] ActiveMembershipStatuses =
        { TeamMemberStatuses.Pending, TeamMemberStatuses.Accepted };

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public TeamService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<TeamResponse> CreateTeamAsync(
        Guid classId, CreateTeamRequest request, CancellationToken ct = default)
    {
        var student = await GetCurrentStudentAsync(ct);
        await EnsureActiveEnrollmentAsync(classId, student.StudentProfileId, ct);

        if (await HasActiveTeamAssociationAsync(classId, student.StudentProfileId, null, ct))
            throw new ConflictException("Bạn đã là thành viên, đang có lời mời hoặc đang là trưởng nhóm khác trong lớp này");

        var teamName = request.TeamName.Trim();
        var normalizedName = teamName.ToLower();
        if (await _uow.Repository<Team>().QueryNoTracking()
                .AnyAsync(t => t.ClassId == classId && t.TeamName.ToLower() == normalizedName, ct))
            throw new ConflictException("Tên nhóm đã tồn tại trong lớp này");

        var now = DateTime.UtcNow;
        var team = new Team
        {
            TeamId = Guid.NewGuid(),
            ClassId = classId,
            LeaderId = student.StudentProfileId,
            TeamName = teamName,
            CreatedAt = now,
            UpdatedAt = now,
        };
        team.TeamMembers.Add(new TeamMember
        {
            TeamId = team.TeamId,
            StudentId = student.StudentProfileId,
            Status = TeamMemberStatuses.Accepted,
            InvitedAt = now,
            RespondedAt = now,
        });

        try
        {
            await _uow.Repository<Team>().AddAsync(team, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Tên nhóm đã tồn tại hoặc thông tin thành viên bị trùng");
        }

        return TeamMappings.ToResponse(await LoadTeamAsync(team.TeamId, ct));
    }

    public async Task<TeamResponse> GetMyTeamAsync(Guid classId, CancellationToken ct = default)
    {
        var student = await GetCurrentStudentAsync(ct);
        await EnsureActiveEnrollmentAsync(classId, student.StudentProfileId, ct);

        var teamIds = await _uow.Repository<Team>().QueryNoTracking()
            .Where(t => t.ClassId == classId &&
                        (t.LeaderId == student.StudentProfileId ||
                         t.TeamMembers.Any(tm => tm.StudentId == student.StudentProfileId &&
                                                 tm.Status == TeamMemberStatuses.Accepted)))
            .Select(t => t.TeamId)
            .Take(2)
            .ToListAsync(ct);

        if (teamIds.Count == 0)
            throw new NotFoundException("Bạn chưa thuộc nhóm nào trong lớp này");
        if (teamIds.Count > 1)
            throw new ConflictException("Dữ liệu không nhất quán: sinh viên thuộc nhiều nhóm trong cùng lớp");

        return TeamMappings.ToResponse(await LoadTeamAsync(teamIds[0], ct));
    }

    public async Task<IReadOnlyList<TeamCandidateResponse>> GetCandidatesAsync(
        Guid classId, CancellationToken ct = default)
    {
        var student = await GetCurrentStudentAsync(ct);
        await EnsureActiveEnrollmentAsync(classId, student.StudentProfileId, ct);

        var isLeader = await _uow.Repository<Team>().QueryNoTracking()
            .AnyAsync(t => t.ClassId == classId && t.LeaderId == student.StudentProfileId, ct);
        if (!isLeader)
            throw new ForbiddenException("Chỉ trưởng nhóm trong lớp mới được xem danh sách sinh viên có thể mời");

        var unavailableMemberIds = _uow.Repository<TeamMember>().QueryNoTracking()
            .Where(tm => tm.Team.ClassId == classId && ActiveMembershipStatuses.Contains(tm.Status))
            .Select(tm => tm.StudentId);
        var leaderIds = _uow.Repository<Team>().QueryNoTracking()
            .Where(t => t.ClassId == classId)
            .Select(t => t.LeaderId);

        return await _uow.Repository<ClassStudent>().QueryNoTracking()
            .Where(cs => cs.ClassId == classId &&
                         cs.Status == StudyingStatus &&
                         cs.StudentId != student.StudentProfileId &&
                         cs.Student.User.IsActive &&
                         !unavailableMemberIds.Contains(cs.StudentId) &&
                         !leaderIds.Contains(cs.StudentId))
            .OrderBy(cs => cs.Student.StudentCode)
            .Select(cs => new TeamCandidateResponse
            {
                StudentProfileId = cs.StudentId,
                StudentCode = cs.Student.StudentCode,
                FullName = cs.Student.User.FullName,
                Major = cs.Student.Major,
                Cohort = cs.Student.Cohort,
            })
            .ToListAsync(ct);
    }

    public async Task<TeamResponse> GetTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var student = await GetCurrentStudentAsync(ct);
        var team = await LoadTeamAsync(teamId, ct);

        var canView = team.LeaderId == student.StudentProfileId ||
                      team.TeamMembers.Any(tm => tm.StudentId == student.StudentProfileId &&
                                                 tm.Status == TeamMemberStatuses.Accepted);
        if (!canView)
            throw new ForbiddenException("Bạn không phải thành viên đã được chấp nhận của nhóm này");

        return TeamMappings.ToResponse(team);
    }

    public async Task<TeamResponse> InviteMemberAsync(
        Guid teamId, InviteTeamMemberRequest request, CancellationToken ct = default)
    {
        var leader = await GetCurrentStudentAsync(ct);
        var team = await GetTeamForLeaderAsync(teamId, leader.StudentProfileId, ct);
        await EnsureActiveEnrollmentAsync(team.ClassId, leader.StudentProfileId, ct);

        if (request.StudentProfileId == leader.StudentProfileId)
            throw new BadRequestException("Trưởng nhóm đã là thành viên của nhóm");

        var candidateIsEligible = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .AnyAsync(cs => cs.ClassId == team.ClassId &&
                            cs.StudentId == request.StudentProfileId &&
                            cs.Status == StudyingStatus &&
                            cs.Student.User.IsActive, ct);
        if (!candidateIsEligible)
            throw new BadRequestException("Sinh viên không tồn tại, không hoạt động hoặc không thuộc lớp này");

        var members = _uow.Repository<TeamMember>();
        var existingInvitation = await members.Query()
            .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.StudentId == request.StudentProfileId, ct);

        if (existingInvitation?.Status == TeamMemberStatuses.Pending)
            throw new ConflictException("Sinh viên đã có lời mời đang chờ phản hồi từ nhóm này");
        if (existingInvitation?.Status == TeamMemberStatuses.Accepted)
            throw new ConflictException("Sinh viên đã là thành viên của nhóm này");

        if (await HasActiveTeamAssociationAsync(team.ClassId, request.StudentProfileId, teamId, ct))
            throw new ConflictException("Sinh viên đã là thành viên, đang có lời mời hoặc đang là trưởng nhóm khác trong lớp này");

        var now = DateTime.UtcNow;
        if (existingInvitation is null)
        {
            await members.AddAsync(new TeamMember
            {
                TeamId = teamId,
                StudentId = request.StudentProfileId,
                Status = TeamMemberStatuses.Pending,
                InvitedAt = now,
                RespondedAt = null,
            }, ct);
        }
        else
        {
            existingInvitation.Status = TeamMemberStatuses.Pending;
            existingInvitation.InvitedAt = now;
            existingInvitation.RespondedAt = null;
        }

        team.UpdatedAt = now;
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Lời mời thành viên đã tồn tại");
        }

        return TeamMappings.ToResponse(await LoadTeamAsync(teamId, ct));
    }

    public async Task<TeamResponse> RemoveUnacceptedMemberAsync(
        Guid teamId, Guid studentProfileId, CancellationToken ct = default)
    {
        var leader = await GetCurrentStudentAsync(ct);
        var team = await GetTeamForLeaderAsync(teamId, leader.StudentProfileId, ct);

        if (studentProfileId == team.LeaderId)
            throw new BadRequestException("Không thể xóa trưởng nhóm");

        var members = _uow.Repository<TeamMember>();
        var member = await members.Query()
                         .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.StudentId == studentProfileId, ct)
                     ?? throw new NotFoundException("Không tìm thấy lời mời của sinh viên trong nhóm");

        if (member.Status == TeamMemberStatuses.Accepted)
            throw new ConflictException("Không thể xóa thành viên đã chấp nhận lời mời trong phạm vi hiện tại");

        members.Remove(member);
        team.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return TeamMappings.ToResponse(await LoadTeamAsync(teamId, ct));
    }

    public async Task<IReadOnlyList<TeamInvitationResponse>> GetMyInvitationsAsync(
        CancellationToken ct = default)
    {
        var student = await GetCurrentStudentAsync(ct);

        var invitations = await _uow.Repository<TeamMember>().QueryNoTracking()
            .Where(tm => tm.StudentId == student.StudentProfileId &&
                         tm.Status == TeamMemberStatuses.Pending &&
                         tm.Team.Class.Semester.IsActive &&
                         tm.Team.Class.ClassStudents.Any(cs => cs.StudentId == student.StudentProfileId &&
                                                              cs.Status == StudyingStatus))
            .Include(tm => tm.Team).ThenInclude(t => t.Class)
            .Include(tm => tm.Team).ThenInclude(t => t.Leader).ThenInclude(s => s.User)
            .OrderByDescending(tm => tm.InvitedAt)
            .AsSplitQuery()
            .ToListAsync(ct);

        return invitations.Select(TeamMappings.ToInvitationResponse).ToList();
    }

    public async Task<TeamResponse> RespondToInvitationAsync(
        Guid teamId, RespondTeamInvitationRequest request, CancellationToken ct = default)
    {
        var student = await GetCurrentStudentAsync(ct);
        var invitation = await _uow.Repository<TeamMember>().Query()
                             .Include(tm => tm.Team).ThenInclude(t => t.Class).ThenInclude(c => c.Semester)
                             .FirstOrDefaultAsync(tm => tm.TeamId == teamId &&
                                                        tm.StudentId == student.StudentProfileId, ct)
                         ?? throw new NotFoundException("Không tìm thấy lời mời vào nhóm");

        if (invitation.Status != TeamMemberStatuses.Pending)
            throw new ConflictException("Lời mời này đã được phản hồi");

        await EnsureActiveEnrollmentAsync(invitation.Team.ClassId, student.StudentProfileId, ct);

        var decision = request.Decision.Equals(TeamMemberStatuses.Accepted, StringComparison.OrdinalIgnoreCase)
            ? TeamMemberStatuses.Accepted
            : TeamMemberStatuses.Rejected;

        if (decision == TeamMemberStatuses.Accepted &&
            await HasActiveTeamAssociationAsync(
                invitation.Team.ClassId, student.StudentProfileId, invitation.TeamId, ct))
            throw new ConflictException("Bạn đã là thành viên, đang có lời mời hoặc đang là trưởng nhóm khác trong lớp này");

        var now = DateTime.UtcNow;
        invitation.Status = decision;
        invitation.RespondedAt = now;
        invitation.Team.UpdatedAt = now;
        await _uow.SaveChangesAsync(ct);

        return TeamMappings.ToResponse(await LoadTeamAsync(teamId, ct));
    }

    private async Task<StudentProfile> GetCurrentStudentAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        return await _uow.Repository<StudentProfile>().QueryNoTracking()
                   .Include(s => s.User)
                   .FirstOrDefaultAsync(s => s.UserId == userId && s.User.IsActive, ct)
               ?? throw new ForbiddenException("Tài khoản hiện tại không có hồ sơ sinh viên hoạt động");
    }

    private async Task EnsureActiveEnrollmentAsync(Guid classId, Guid studentId, CancellationToken ct)
    {
        var classInfo = await _uow.Repository<Class>().QueryNoTracking()
                            .Include(c => c.Semester)
                            .FirstOrDefaultAsync(c => c.ClassId == classId, ct)
                        ?? throw new NotFoundException("Không tìm thấy lớp học");

        if (!classInfo.Semester.IsActive)
            throw new ConflictException("Học kỳ của lớp không còn hoạt động");

        var isEnrolled = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .AnyAsync(cs => cs.ClassId == classId &&
                            cs.StudentId == studentId &&
                            cs.Status == StudyingStatus, ct);
        if (!isEnrolled)
            throw new ForbiddenException("Sinh viên không thuộc lớp hoặc không còn ở trạng thái Studying");
    }

    private async Task<bool> HasActiveTeamAssociationAsync(
        Guid classId, Guid studentId, Guid? excludedTeamId, CancellationToken ct)
    {
        var leadsAnotherTeam = await _uow.Repository<Team>().QueryNoTracking()
            .AnyAsync(t => t.ClassId == classId &&
                           t.LeaderId == studentId &&
                           (!excludedTeamId.HasValue || t.TeamId != excludedTeamId.Value), ct);
        if (leadsAnotherTeam)
            return true;

        return await _uow.Repository<TeamMember>().QueryNoTracking()
            .AnyAsync(tm => tm.Team.ClassId == classId &&
                            tm.StudentId == studentId &&
                            ActiveMembershipStatuses.Contains(tm.Status) &&
                            (!excludedTeamId.HasValue || tm.TeamId != excludedTeamId.Value), ct);
    }

    private async Task<Team> GetTeamForLeaderAsync(Guid teamId, Guid studentId, CancellationToken ct)
    {
        var team = await _uow.Repository<Team>().Query()
                       .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
                   ?? throw new NotFoundException("Không tìm thấy nhóm");

        if (team.LeaderId != studentId)
            throw new ForbiddenException("Chỉ trưởng nhóm mới được thực hiện thao tác này");

        return team;
    }

    private async Task<Team> LoadTeamAsync(Guid teamId, CancellationToken ct)
        => await _uow.Repository<Team>().QueryNoTracking()
               .Include(t => t.Class)
               .Include(t => t.Leader).ThenInclude(s => s.User)
               .Include(t => t.TeamMembers).ThenInclude(tm => tm.Student).ThenInclude(s => s.User)
               .AsSplitQuery()
               .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
           ?? throw new NotFoundException("Không tìm thấy nhóm");
}

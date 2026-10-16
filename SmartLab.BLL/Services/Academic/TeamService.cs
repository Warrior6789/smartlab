using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class TeamService : ITeamService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public TeamService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    private async Task<StudentProfile> GetCurrentStudentProfileAsync(CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            throw new UnauthorizedException();

        var profile = await _uow.Repository<StudentProfile>().Query()
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.UserId == _currentUser.UserId.Value, ct);

        if (profile == null)
            throw new ForbiddenException("Chỉ tài khoản sinh viên mới có quyền thực hiện thao tác này.");

        return profile;
    }

    public async Task<List<AvailableStudentResponse>> GetAvailableStudentsAsync(Guid classId, string? search = null, CancellationToken ct = default)
    {
        var currentStudent = await GetCurrentStudentProfileAsync(ct);

        // Lấy danh sách ID sinh viên đã có nhóm trong lớp này (Leader hoặc Member đã Accepted)
        var busyLeaderIds = await _uow.Repository<Team>().QueryNoTracking()
            .Where(t => t.ClassId == classId)
            .Select(t => t.LeaderId)
            .ToListAsync(ct);

        var busyMemberIds = await _uow.Repository<TeamMember>().QueryNoTracking()
            .Where(tm => tm.Team.ClassId == classId && tm.Status == "Accepted")
            .Select(tm => tm.StudentId)
            .ToListAsync(ct);

        var excludeStudentIds = busyLeaderIds
            .Concat(busyMemberIds)
            .Append(currentStudent.StudentProfileId) // Loại trừ chính mình
            .ToHashSet();

        // Lấy các sinh viên đang học trong lớp và chưa có nhóm
        var query = _uow.Repository<ClassStudent>().QueryNoTracking()
            .Include(cs => cs.Student).ThenInclude(s => s.User)
            .Where(cs => cs.ClassId == classId 
                      && cs.Status == "Studying" 
                      && !excludeStudentIds.Contains(cs.StudentId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(cs => cs.Student.StudentCode.ToLower().Contains(term) 
                                   || cs.Student.User.FullName.ToLower().Contains(term));
        }

        var list = await query
            .OrderBy(cs => cs.Student.StudentCode)
            .ToListAsync(ct);

        return list.Select(cs => new AvailableStudentResponse
        {
            StudentProfileId = cs.Student.StudentProfileId,
            StudentCode = cs.Student.StudentCode,
            FullName = cs.Student.User.FullName,
            Email = cs.Student.User.Email
        }).ToList();
    }

    public async Task<TeamResponse> CreateTeamAsync(CreateTeamRequest request, CancellationToken ct = default)
    {
        var currentStudent = await GetCurrentStudentProfileAsync(ct);

        // 1. Kiểm tra sinh viên có trong lớp và đang học không
        var isEnrolled = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .AnyAsync(cs => cs.ClassId == request.ClassId && cs.StudentId == currentStudent.StudentProfileId && cs.Status == "Studying", ct);
        if (!isEnrolled)
            throw new BadRequestException("Bạn chưa ghi danh hoặc không có quyền tạo nhóm trong lớp học này.");

        // 2. Kiểm tra sinh viên đã ở trong nhóm nào của lớp này chưa
        var isAlreadyLeader = await _uow.Repository<Team>().QueryNoTracking()
            .AnyAsync(t => t.ClassId == request.ClassId && t.LeaderId == currentStudent.StudentProfileId, ct);
        if (isAlreadyLeader)
            throw new ConflictException("Bạn đã là trưởng nhóm của một nhóm khác trong lớp này.");

        var isAlreadyMember = await _uow.Repository<TeamMember>().QueryNoTracking()
            .AnyAsync(tm => tm.Team.ClassId == request.ClassId && tm.StudentId == currentStudent.StudentProfileId && tm.Status == "Accepted", ct);
        if (isAlreadyMember)
            throw new ConflictException("Bạn đã tham gia một nhóm khác trong lớp học này.");

        // 3. Kiểm tra tên nhóm trùng trong lớp
        var nameExists = await _uow.Repository<Team>().QueryNoTracking()
            .AnyAsync(t => t.ClassId == request.ClassId && t.TeamName.ToLower() == request.TeamName.Trim().ToLower(), ct);
        if (nameExists)
            throw new ConflictException($"Tên nhóm '{request.TeamName.Trim()}' đã tồn tại trong lớp này.");

        var now = DateTime.UtcNow;
        var teamId = Guid.NewGuid();
        var team = new Team
        {
            TeamId = teamId,
            ClassId = request.ClassId,
            LeaderId = currentStudent.StudentProfileId,
            TeamName = request.TeamName.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _uow.Repository<Team>().AddAsync(team, ct);

        // 4. Nếu có truyền danh sách thành viên muốn mời ngay từ màn hình tạo nhóm
        if (request.InvitedStudentCodes != null && request.InvitedStudentCodes.Any())
        {
            var distinctCodes = request.InvitedStudentCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            foreach (var code in distinctCodes)
            {
                var target = await _uow.Repository<StudentProfile>().QueryNoTracking()
                    .FirstOrDefaultAsync(sp => sp.StudentCode == code, ct);

                if (target == null || target.StudentProfileId == currentStudent.StudentProfileId)
                    continue;

                // Kiểm tra sinh viên có trong lớp không
                var enrolled = await _uow.Repository<ClassStudent>().QueryNoTracking()
                    .AnyAsync(cs => cs.ClassId == request.ClassId && cs.StudentId == target.StudentProfileId && cs.Status == "Studying", ct);
                if (!enrolled)
                    continue;

                // Kiểm tra sinh viên đã có nhóm chưa
                var hasTeam = await _uow.Repository<Team>().QueryNoTracking().AnyAsync(t => t.ClassId == request.ClassId && t.LeaderId == target.StudentProfileId, ct)
                           || await _uow.Repository<TeamMember>().QueryNoTracking().AnyAsync(tm => tm.Team.ClassId == request.ClassId && tm.StudentId == target.StudentProfileId && tm.Status == "Accepted", ct);
                if (hasTeam)
                    continue;

                var invite = new TeamMember
                {
                    TeamId = teamId,
                    StudentId = target.StudentProfileId,
                    Status = "Pending",
                    InvitedAt = now
                };
                await _uow.Repository<TeamMember>().AddAsync(invite, ct);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return await GetTeamByIdAsync(team.TeamId, ct);
    }

    public async Task<TeamResponse> InviteMemberAsync(Guid teamId, InviteMemberRequest request, CancellationToken ct = default)
    {
        var currentStudent = await GetCurrentStudentProfileAsync(ct);

        var team = await _uow.Repository<Team>().Query()
            .Include(t => t.Class)
            .Include(t => t.TeamMembers)
            .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
            ?? throw new NotFoundException("Nhóm", teamId);

        // Chỉ trưởng nhóm mới được mời
        if (team.LeaderId != currentStudent.StudentProfileId)
            throw new ForbiddenException("Chỉ trưởng nhóm mới có quyền mời thành viên.");

        // Tìm sinh viên được mời theo Mã sinh viên
        var targetStudent = await _uow.Repository<StudentProfile>().Query()
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.StudentCode == request.StudentCode.Trim().ToUpperInvariant(), ct)
            ?? throw new NotFoundException($"Không tìm thấy sinh viên có mã '{request.StudentCode}'.");

        if (targetStudent.StudentProfileId == currentStudent.StudentProfileId)
            throw new BadRequestException("Bạn không thể tự mời chính mình.");

        // Kiểm tra sinh viên có học cùng lớp không
        var isTargetEnrolled = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .AnyAsync(cs => cs.ClassId == team.ClassId && cs.StudentId == targetStudent.StudentProfileId && cs.Status == "Studying", ct);
        if (!isTargetEnrolled)
            throw new BadRequestException($"Sinh viên '{request.StudentCode}' không thuộc lớp học của nhóm bạn.");

        // Kiểm tra sinh viên đã có nhóm trong lớp này chưa
        var isTargetLeader = await _uow.Repository<Team>().QueryNoTracking()
            .AnyAsync(t => t.ClassId == team.ClassId && t.LeaderId == targetStudent.StudentProfileId, ct);
        if (isTargetLeader)
            throw new ConflictException($"Sinh viên '{request.StudentCode}' đã là trưởng một nhóm khác trong lớp.");

        var isTargetInTeam = await _uow.Repository<TeamMember>().QueryNoTracking()
            .AnyAsync(tm => tm.Team.ClassId == team.ClassId && tm.StudentId == targetStudent.StudentProfileId && tm.Status == "Accepted", ct);
        if (isTargetInTeam)
            throw new ConflictException($"Sinh viên '{request.StudentCode}' đã tham gia một nhóm khác trong lớp.");

        // Kiểm tra trạng thái lời mời trước đó
        var existingInvite = team.TeamMembers.FirstOrDefault(tm => tm.StudentId == targetStudent.StudentProfileId);
        if (existingInvite != null)
        {
            if (existingInvite.Status == "Pending")
                throw new ConflictException("Đã gửi lời mời đến sinh viên này rồi, đang chờ phản hồi.");

            existingInvite.Status = "Pending";
            existingInvite.InvitedAt = DateTime.UtcNow;
            existingInvite.RespondedAt = null;
        }
        else
        {
            var newInvite = new TeamMember
            {
                TeamId = teamId,
                StudentId = targetStudent.StudentProfileId,
                Status = "Pending",
                InvitedAt = DateTime.UtcNow
            };
            await _uow.Repository<TeamMember>().AddAsync(newInvite, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return await GetTeamByIdAsync(teamId, ct);
    }

    public async Task<List<MyInvitationResponse>> GetMyInvitationsAsync(CancellationToken ct = default)
    {
        var currentStudent = await GetCurrentStudentProfileAsync(ct);

        var invites = await _uow.Repository<TeamMember>().QueryNoTracking()
            .Include(tm => tm.Team).ThenInclude(t => t.Class)
            .Include(tm => tm.Team).ThenInclude(t => t.Leader).ThenInclude(l => l.User)
            .Where(tm => tm.StudentId == currentStudent.StudentProfileId && tm.Status == "Pending")
            .OrderByDescending(tm => tm.InvitedAt)
            .ToListAsync(ct);

        return invites.Select(i => new MyInvitationResponse
        {
            TeamId = i.TeamId,
            TeamName = i.Team.TeamName,
            ClassId = i.Team.ClassId,
            ClassCode = i.Team.Class.ClassCode,
            LeaderName = i.Team.Leader.User.FullName,
            InvitedAt = i.InvitedAt
        }).ToList();
    }

    public async Task RespondInvitationAsync(Guid teamId, RespondInvitationRequest request, CancellationToken ct = default)
    {
        var currentStudent = await GetCurrentStudentProfileAsync(ct);

        var invite = await _uow.Repository<TeamMember>().Query()
            .Include(tm => tm.Team)
            .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.StudentId == currentStudent.StudentProfileId, ct)
            ?? throw new NotFoundException("Không tìm thấy lời mời vào nhóm này.");

        if (invite.Status != "Pending")
            throw new BadRequestException("Lời mời này không còn ở trạng thái chờ phản hồi.");

        if (request.Accept)
        {
            var alreadyInTeam = await _uow.Repository<TeamMember>().QueryNoTracking()
                .AnyAsync(tm => tm.Team.ClassId == invite.Team.ClassId && tm.StudentId == currentStudent.StudentProfileId && tm.Status == "Accepted", ct);
            if (alreadyInTeam)
                throw new ConflictException("Bạn đã tham gia một nhóm khác trong lớp này rồi.");

            invite.Status = "Accepted";
            invite.RespondedAt = DateTime.UtcNow;

            // Tự động từ chối các lời mời khác trong cùng lớp
            var otherPending = await _uow.Repository<TeamMember>().Query()
                .Where(tm => tm.StudentId == currentStudent.StudentProfileId
                          && tm.TeamId != teamId
                          && tm.Team.ClassId == invite.Team.ClassId
                          && tm.Status == "Pending")
                .ToListAsync(ct);

            foreach (var other in otherPending)
            {
                other.Status = "Rejected";
                other.RespondedAt = DateTime.UtcNow;
            }
        }
        else
        {
            invite.Status = "Rejected";
            invite.RespondedAt = DateTime.UtcNow;
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<List<TeamResponse>> GetTeamsByClassAsync(Guid classId, CancellationToken ct = default)
    {
        var teams = await _uow.Repository<Team>().QueryNoTracking()
            .Include(t => t.Class)
            .Include(t => t.Leader).ThenInclude(l => l.User)
            .Include(t => t.TeamMembers).ThenInclude(tm => tm.Student).ThenInclude(s => s.User)
            .Where(t => t.ClassId == classId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        return teams.Select(MapToResponse).ToList();
    }

    public async Task<TeamResponse> GetTeamByIdAsync(Guid teamId, CancellationToken ct = default)
    {
        var team = await _uow.Repository<Team>().QueryNoTracking()
            .Include(t => t.Class)
            .Include(t => t.Leader).ThenInclude(l => l.User)
            .Include(t => t.TeamMembers).ThenInclude(tm => tm.Student).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
            ?? throw new NotFoundException("Nhóm", teamId);

        return MapToResponse(team);
    }

    private static TeamResponse MapToResponse(Team team)
    {
        var response = new TeamResponse
        {
            TeamId = team.TeamId,
            ClassId = team.ClassId,
            ClassCode = team.Class.ClassCode,
            TeamName = team.TeamName,
            LeaderId = team.LeaderId,
            LeaderCode = team.Leader.StudentCode,
            LeaderName = team.Leader.User.FullName,
            CreatedAt = team.CreatedAt
        };

        // Thêm Trưởng nhóm vào danh sách hiển thị
        response.Members.Add(new TeamMemberItemResponse
        {
            StudentId = team.LeaderId,
            StudentCode = team.Leader.StudentCode,
            FullName = team.Leader.User.FullName,
            Email = team.Leader.User.Email,
            RoleInTeam = "Leader",
            Status = "Accepted",
            JoinedOrInvitedAt = team.CreatedAt
        });

        // Thêm các thành viên được mời
        foreach (var m in team.TeamMembers)
        {
            response.Members.Add(new TeamMemberItemResponse
            {
                StudentId = m.StudentId,
                StudentCode = m.Student.StudentCode,
                FullName = m.Student.User.FullName,
                Email = m.Student.User.Email,
                RoleInTeam = "Member",
                Status = m.Status,
                JoinedOrInvitedAt = m.InvitedAt
            });
        }

        // Đếm tổng số thành viên chính thức (Leader + Accepted)
        response.MemberCount = response.Members.Count(m => m.Status == "Accepted");

        return response;
    }
}
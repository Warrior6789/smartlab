using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Teamwork;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.BLL.Interfaces.Teamwork;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Teamwork;

public class TeamFormationService : ITeamFormationService
{
    private const string ChangedMessage = "Lớp vừa thay đổi, vui lòng tải lại";
    private const string NotLockedMessage = "Chỉ xếp nhóm được sau khi lớp đã chốt nhóm";
    private const string ExceedMaxKey = "allowExceedMax";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationSender _notifications;
    private readonly IChangeBroadcaster _changes;

    public TeamFormationService(IUnitOfWork uow, ICurrentUserService currentUser,
        INotificationSender notifications, IChangeBroadcaster changes)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _changes = changes;
    }

    public async Task<TeamFormationResponse> GetAsync(Guid classId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        if (await IsManagerAsync(classId, userId, ct))
            return await BuildResponseAsync(classId, includeDetails: true, ct);

        var isStudent = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .AnyAsync(cs => cs.ClassId == classId && cs.Student.UserId == userId &&
                            cs.Status == ClassStudentStatuses.Studying, ct);
        if (!isStudent)
            throw new ForbiddenException("Bạn không thuộc lớp này");

        return await BuildResponseAsync(classId, includeDetails: false, ct);
    }

    public async Task<TeamFormationResponse> UpdateSettingsAsync(
        Guid classId, UpdateTeamFormationSettingsRequest request, CancellationToken ct = default)
    {
        await EnsureManagerAsync(classId, ct);
        await RunInTransactionAsync(async token =>
        {
            var cls = await LockClassAsync(classId, token);

            if (cls.TeamFormationStatus == TeamFormationStatuses.Open)
            {
                // Students are mid-selection: a team that already has (or is inviting) more people may not become invalid.
                var largest = await _uow.Repository<TeamMember>().QueryNoTracking()
                    .Where(m => m.Team.ClassId == classId &&
                                (m.Status == TeamMemberStatuses.Accepted || m.Status == TeamMemberStatuses.Pending))
                    .GroupBy(m => m.TeamId)
                    .Select(g => g.Count())
                    .OrderByDescending(n => n)
                    .FirstOrDefaultAsync(token);
                if (request.MaxTeamSize < largest)
                    throw new ConflictException(
                        $"Đang có nhóm {largest} thành viên (tính cả lời mời đang chờ), số tối đa không được nhỏ hơn {largest}");
            }

            cls.MinTeamSize = request.MinTeamSize;
            cls.MaxTeamSize = request.MaxTeamSize;
            cls.TeamFormationDeadline = request.Deadline is { } deadline ? ToUtc(deadline) : null;
            await _uow.SaveChangesAsync(token);
            return true;
        }, ct);

        return await BuildResponseAsync(classId, includeDetails: true, ct);
    }

    public async Task<TeamFormationResponse> OpenAsync(Guid classId, CancellationToken ct = default)
    {
        await EnsureManagerAsync(classId, ct);
        var classCode = await RunInTransactionAsync(async token =>
        {
            var cls = await LockClassAsync(classId, token);
            if (cls.TeamFormationStatus == TeamFormationStatuses.Open)
                throw new ConflictException("Lớp đang mở chia nhóm");
            if (cls.MinTeamSize is null || cls.MaxTeamSize is null)
                throw new BadRequestException("Hãy thiết lập số thành viên tối thiểu và tối đa của nhóm trước khi mở chia nhóm");
            if (cls.TeamFormationDeadline is { } deadline && deadline <= DateTime.UtcNow)
                throw new BadRequestException("Hạn chót chia nhóm đã qua, hãy cập nhật hạn chót mới hoặc bỏ hạn chót");

            var semesterActive = await _uow.Repository<Semester>().QueryNoTracking()
                .AnyAsync(s => s.SemesterId == cls.SemesterId && s.IsActive, token);
            if (!semesterActive)
                throw new ConflictException("Học kỳ của lớp không còn hoạt động");

            cls.TeamFormationStatus = TeamFormationStatuses.Open;
            cls.TeamFormationOpenedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(token);
            return cls.ClassCode;
        }, ct);

        await NotifyStudyingStudentsAsync(classId,
            $"SmartLab - Lớp {classCode} đã mở chia nhóm, hãy tạo nhóm hoặc nhận lời mời vào nhóm");
        return await BuildResponseAsync(classId, includeDetails: true, ct);
    }

    public async Task<TeamFormationResponse> LockAsync(Guid classId, CancellationToken ct = default)
    {
        await EnsureManagerAsync(classId, ct);
        var (classCode, touchedTeams) = await RunInTransactionAsync(async token =>
        {
            var cls = await LockClassAsync(classId, token);
            if (cls.TeamFormationStatus == TeamFormationStatuses.Locked)
                throw new ConflictException("Lớp đã chốt nhóm");
            if (cls.MinTeamSize is null || cls.MaxTeamSize is null)
                throw new BadRequestException("Hãy thiết lập số thành viên tối thiểu và tối đa của nhóm trước khi chốt nhóm");

            // No invitation stays pending after the lock: teams consist of Accepted members only.
            var now = DateTime.UtcNow;
            var pending = _uow.Repository<TeamMember>().Query()
                .Where(m => m.Team.ClassId == classId && m.Status == TeamMemberStatuses.Pending);
            var teamIds = await pending.Select(m => m.TeamId).Distinct().ToListAsync(token);
            await pending.ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, TeamMemberStatuses.Rejected)
                .SetProperty(m => m.RespondedAt, now), token);
            await _uow.Repository<Team>().Query()
                .Where(t => teamIds.Contains(t.TeamId))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, now), token);

            cls.TeamFormationStatus = TeamFormationStatuses.Locked;
            await _uow.SaveChangesAsync(token);
            return (cls.ClassCode, teamIds);
        }, ct);

        foreach (var teamId in touchedTeams)
            await _changes.TeamChangedAsync(teamId);
        await NotifyStudyingStudentsAsync(classId,
            $"SmartLab - Lớp {classCode} đã chốt danh sách nhóm, giảng viên sẽ xếp nhóm cho sinh viên chưa có nhóm");
        return await BuildResponseAsync(classId, includeDetails: true, ct);
    }

    public async Task<AutoAssignPreviewResponse> PreviewAutoAssignAsync(Guid classId, CancellationToken ct = default)
    {
        var cls = await EnsureManagerAsync(classId, ct);
        if (cls.TeamFormationStatus != TeamFormationStatuses.Locked)
            throw new ConflictException(NotLockedMessage);
        int min = cls.MinTeamSize!.Value, max = cls.MaxTeamSize!.Value;

        var teams = await _uow.Repository<Team>().QueryNoTracking()
            .Where(t => t.ClassId == classId && t.Project == null)
            .Select(t => new
            {
                t.TeamId,
                t.TeamName,
                Count = t.TeamMembers.Count(m => m.Status == TeamMemberStatuses.Accepted),
            })
            .ToListAsync(ct);
        var students = await UnassignedStudentsQuery(classId)
            .Select(cs => new TeamFormationStudentItem
            {
                StudentProfileId = cs.StudentId,
                StudentCode = cs.Student.StudentCode,
                FullName = cs.Student.User.FullName,
            })
            .ToListAsync(ct);

        if (students.Count == 0)
            return new AutoAssignPreviewResponse { Warnings = new[] { "Không còn sinh viên nào chưa có nhóm" } };

        var shuffled = students.OrderBy(_ => Random.Shared.Next()).ToList();
        var byId = shuffled.ToDictionary(s => s.StudentProfileId);
        var plan = TeamAutoAssigner.Build(
            teams.Select(t => new TeamAutoAssigner.TeamSlot(t.TeamId, t.Count)).ToList(),
            shuffled.Select(s => s.StudentProfileId).ToList(), min, max);

        var allNames = await _uow.Repository<Team>().QueryNoTracking()
            .Where(t => t.ClassId == classId).Select(t => t.TeamName.ToLower()).ToListAsync(ct);
        var usedNames = new HashSet<string>(allNames);
        var nextNumber = 1;
        string NextName()
        {
            while (usedNames.Contains($"nhóm {nextNumber}"))
                nextNumber++;
            usedNames.Add($"nhóm {nextNumber}");
            return $"Nhóm {nextNumber++}";
        }

        return new AutoAssignPreviewResponse
        {
            ExistingTeams = teams
                .Where(t => plan.ToExistingTeams.ContainsKey(t.TeamId))
                .Select(t => new AutoAssignExistingTeam
                {
                    TeamId = t.TeamId,
                    TeamName = t.TeamName,
                    CurrentCount = t.Count,
                    AddedStudents = plan.ToExistingTeams[t.TeamId].Select(id => byId[id]).ToList(),
                })
                .OrderBy(t => t.TeamName)
                .ToList(),
            NewTeams = plan.NewTeams
                .Select(ids => new AutoAssignNewTeam
                {
                    TeamName = NextName(),
                    LeaderStudentProfileId = ids[0],
                    Students = ids.Select(id => byId[id]).ToList(),
                })
                .ToList(),
            RequiresExceedMax = plan.ExceedsMax,
            Warnings = plan.Warnings,
        };
    }

    public async Task<TeamFormationResponse> ApplyAssignmentsAsync(
        Guid classId, ApplyTeamAssignmentsRequest request, CancellationToken ct = default)
    {
        await EnsureManagerAsync(classId, ct);

        var allStudentIds = request.ExistingTeams.SelectMany(t => t.StudentProfileIds)
            .Concat(request.NewTeams.SelectMany(t => t.StudentProfileIds))
            .ToList();
        if (allStudentIds.Count != allStudentIds.Distinct().Count())
            throw new BadRequestException("Một sinh viên chỉ được xếp vào một nhóm");
        if (request.ExistingTeams.Select(t => t.TeamId).Distinct().Count() != request.ExistingTeams.Count)
            throw new BadRequestException("Mỗi nhóm chỉ được xuất hiện một lần trong danh sách");
        var newNames = request.NewTeams.Select(t => t.TeamName.Trim()).ToList();
        if (newNames.Select(n => n.ToLower()).Distinct().Count() != newNames.Count)
            throw new BadRequestException("Tên các nhóm mới bị trùng nhau");

        var (changedTeams, placed) = await RunInTransactionAsync(async token =>
        {
            var cls = await TeamFormationGate.EnsureLockedAsync(_uow, classId, NotLockedMessage, token);
            var max = cls.MaxTeamSize!.Value;
            var now = DateTime.UtcNow;

            var eligible = await UnassignedStudentsQuery(classId)
                .Where(cs => allStudentIds.Contains(cs.StudentId))
                .Select(cs => cs.StudentId)
                .ToListAsync(token);
            var invalid = allStudentIds.Except(eligible).ToList();
            if (invalid.Count > 0)
                throw new BadRequestException(
                    $"Có {invalid.Count} sinh viên không thuộc lớp, không còn Studying hoặc đã có nhóm",
                    new Dictionary<string, string[]> { ["studentProfileIds"] = invalid.Select(i => i.ToString()).ToArray() });

            var teamIds = request.ExistingTeams.Select(t => t.TeamId).ToList();
            var teams = await _uow.Repository<Team>().Query()
                .Include(t => t.TeamMembers)
                .Include(t => t.Project)
                .Where(t => teamIds.Contains(t.TeamId))
                .ToListAsync(token);
            var exceeded = new List<string>();
            foreach (var assignment in request.ExistingTeams)
            {
                var team = teams.FirstOrDefault(t => t.TeamId == assignment.TeamId && t.ClassId == classId)
                           ?? throw new NotFoundException("Không tìm thấy nhóm trong lớp này");
                if (team.Project != null)
                    throw new ConflictException($"Nhóm {team.TeamName} đã đăng ký đề tài, không thể thêm thành viên");
                var size = team.TeamMembers.Count(m => m.Status == TeamMemberStatuses.Accepted) + assignment.StudentProfileIds.Count;
                if (size > max)
                    exceeded.Add($"{team.TeamName}: {size}");
            }
            foreach (var (newTeam, name) in request.NewTeams.Zip(newNames))
                if (newTeam.StudentProfileIds.Count > max)
                    exceeded.Add($"{name}: {newTeam.StudentProfileIds.Count}");
            EnsureExceedConfirmed(exceeded, max, request.AllowExceedMax);

            var lowerNames = newNames.Select(n => n.ToLower()).ToList();
            var taken = await _uow.Repository<Team>().QueryNoTracking()
                .Where(t => t.ClassId == classId && lowerNames.Contains(t.TeamName.ToLower()))
                .Select(t => t.TeamName)
                .ToListAsync(token);
            if (taken.Count > 0)
                throw new ConflictException($"Tên nhóm đã tồn tại trong lớp: {string.Join(", ", taken)}");

            var changed = new List<Guid>();
            var placedStudents = new List<(Guid StudentId, Guid TeamId)>();
            foreach (var assignment in request.ExistingTeams.Where(a => a.StudentProfileIds.Count > 0))
            {
                var team = teams.Single(t => t.TeamId == assignment.TeamId);
                foreach (var studentId in assignment.StudentProfileIds)
                {
                    await AddAcceptedMemberAsync(team, studentId, now, token);
                    placedStudents.Add((studentId, team.TeamId));
                }
                team.UpdatedAt = now;
                changed.Add(team.TeamId);
            }

            foreach (var (newTeam, name) in request.NewTeams.Zip(newNames))
            {
                var team = new Team
                {
                    TeamId = Guid.NewGuid(),
                    ClassId = classId,
                    LeaderId = newTeam.LeaderStudentProfileId,
                    TeamName = name,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                foreach (var studentId in newTeam.StudentProfileIds)
                {
                    team.TeamMembers.Add(new TeamMember
                    {
                        TeamId = team.TeamId,
                        StudentId = studentId,
                        Status = TeamMemberStatuses.Accepted,
                        InvitedAt = now,
                        RespondedAt = now,
                    });
                    placedStudents.Add((studentId, team.TeamId));
                }
                await _uow.Repository<Team>().AddAsync(team, token);
                changed.Add(team.TeamId);
            }

            await SaveOrConflictAsync(token);
            return (changed, placedStudents);
        }, ct);

        foreach (var teamId in changedTeams)
            await _changes.TeamChangedAsync(teamId);
        await NotifyPlacedAsync(placed);
        return await BuildResponseAsync(classId, includeDetails: true, ct);
    }

    public async Task<TeamFormationResponse> MoveMemberAsync(
        Guid classId, MoveTeamMemberRequest request, CancellationToken ct = default)
    {
        await EnsureManagerAsync(classId, ct);

        var (sourceTeamId, sourceDeleted) = await RunInTransactionAsync(async token =>
        {
            var cls = await TeamFormationGate.EnsureLockedAsync(_uow, classId, NotLockedMessage, token);
            var max = cls.MaxTeamSize!.Value;
            var now = DateTime.UtcNow;

            var studying = await _uow.Repository<ClassStudent>().QueryNoTracking()
                .AnyAsync(cs => cs.ClassId == classId && cs.StudentId == request.StudentProfileId &&
                                cs.Status == ClassStudentStatuses.Studying, token);
            if (!studying)
                throw new BadRequestException("Sinh viên không thuộc lớp hoặc không còn ở trạng thái Studying");

            var source = await _uow.Repository<Team>().Query()
                             .Include(t => t.TeamMembers)
                             .Include(t => t.Project)
                             .FirstOrDefaultAsync(t => t.ClassId == classId && t.TeamMembers.Any(m =>
                                 m.StudentId == request.StudentProfileId && m.Status == TeamMemberStatuses.Accepted), token)
                         ?? throw new BadRequestException("Sinh viên chưa có nhóm, hãy dùng chức năng xếp nhóm");
            if (source.TeamId == request.TargetTeamId)
                throw new BadRequestException("Sinh viên đã ở trong nhóm này");

            var target = await _uow.Repository<Team>().Query()
                             .Include(t => t.TeamMembers)
                             .Include(t => t.Project)
                             .FirstOrDefaultAsync(t => t.TeamId == request.TargetTeamId && t.ClassId == classId, token)
                         ?? throw new NotFoundException("Không tìm thấy nhóm đích trong lớp này");
            if (source.Project != null || target.Project != null)
                throw new ConflictException("Nhóm đã đăng ký đề tài, không thể thay đổi thành viên");

            var targetSize = target.TeamMembers.Count(m => m.Status == TeamMemberStatuses.Accepted) + 1;
            if (targetSize > max)
                EnsureExceedConfirmed(new List<string> { $"{target.TeamName}: {targetSize}" }, max, request.AllowExceedMax);

            var othersInSource = source.TeamMembers.Count(m => m.Status == TeamMemberStatuses.Accepted &&
                                                               m.StudentId != request.StudentProfileId);
            var deleteSource = false;
            if (source.LeaderId == request.StudentProfileId)
            {
                if (othersInSource > 0)
                    throw new BadRequestException("Sinh viên đang là trưởng nhóm, hãy chuyển trưởng nhóm cho thành viên khác trước");
                deleteSource = true;
            }

            await AddAcceptedMemberAsync(target, request.StudentProfileId, now, token);
            target.UpdatedAt = now;

            if (deleteSource)
            {
                // The student was alone: the team would be empty, so it goes away (Rejected invitation rows included).
                foreach (var member in source.TeamMembers.ToList())
                    _uow.Repository<TeamMember>().Remove(member);
                _uow.Repository<Team>().Remove(source);
            }
            else
            {
                _uow.Repository<TeamMember>().Remove(source.TeamMembers.Single(m => m.StudentId == request.StudentProfileId));
                source.UpdatedAt = now;
            }

            await SaveOrConflictAsync(token);
            return (source.TeamId, deleteSource);
        }, ct);

        await _changes.TeamChangedAsync(request.TargetTeamId);
        if (!sourceDeleted)
            await _changes.TeamChangedAsync(sourceTeamId, request.StudentProfileId);
        await NotifyPlacedAsync(new[] { (request.StudentProfileId, request.TargetTeamId) });
        return await BuildResponseAsync(classId, includeDetails: true, ct);
    }

    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>Studying, active students of the class who are neither a leader nor an Accepted member of any team.</summary>
    private IQueryable<ClassStudent> UnassignedStudentsQuery(Guid classId)
        => _uow.Repository<ClassStudent>().QueryNoTracking()
            .Where(cs => cs.ClassId == classId &&
                         cs.Status == ClassStudentStatuses.Studying &&
                         cs.Student.User.IsActive &&
                         !cs.Class.Teams.Any(t => t.LeaderId == cs.StudentId ||
                                                  t.TeamMembers.Any(m => m.StudentId == cs.StudentId &&
                                                                         m.Status == TeamMemberStatuses.Accepted)))
            .OrderBy(cs => cs.Student.StudentCode);

    /// <summary>Re-uses the student's old (Rejected) row in that team if any: (team_id, student_id) is the primary key.</summary>
    private async Task AddAcceptedMemberAsync(Team team, Guid studentId, DateTime now, CancellationToken ct)
    {
        var existing = team.TeamMembers.FirstOrDefault(m => m.StudentId == studentId);
        if (existing != null)
        {
            existing.Status = TeamMemberStatuses.Accepted;
            existing.InvitedAt = now;
            existing.RespondedAt = now;
            return;
        }

        await _uow.Repository<TeamMember>().AddAsync(new TeamMember
        {
            TeamId = team.TeamId,
            StudentId = studentId,
            Status = TeamMemberStatuses.Accepted,
            InvitedAt = now,
            RespondedAt = now,
        }, ct);
    }

    private static void EnsureExceedConfirmed(List<string> exceeded, int max, bool allowed)
    {
        if (exceeded.Count == 0 || allowed)
            return;
        throw new ConflictException(
            $"Một số nhóm sẽ vượt quá {max} thành viên ({string.Join("; ", exceeded)}). Gửi lại với allowExceedMax = true để xác nhận",
            new Dictionary<string, string[]> { [ExceedMaxKey] = exceeded.ToArray() });
    }

    private async Task<TeamFormationResponse> BuildResponseAsync(Guid classId, bool includeDetails, CancellationToken ct)
    {
        var cls = await _uow.Repository<Class>().QueryNoTracking()
                      .FirstOrDefaultAsync(c => c.ClassId == classId, ct)
                  ?? throw new NotFoundException("Lớp học", classId);

        var teams = await _uow.Repository<Team>().QueryNoTracking()
            .Where(t => t.ClassId == classId)
            .Select(t => new TeamFormationTeamItem
            {
                TeamId = t.TeamId,
                TeamName = t.TeamName,
                LeaderId = t.LeaderId,
                LeaderFullName = t.Leader.User.FullName,
                AcceptedCount = t.TeamMembers.Count(m => m.Status == TeamMemberStatuses.Accepted),
                PendingCount = t.TeamMembers.Count(m => m.Status == TeamMemberStatuses.Pending),
                HasProject = t.Project != null,
                Members = t.TeamMembers
                    .Where(m => m.Status == TeamMemberStatuses.Accepted)
                    .OrderBy(m => m.Student.StudentCode)
                    .Select(m => new TeamFormationStudentItem
                    {
                        StudentProfileId = m.StudentId,
                        StudentCode = m.Student.StudentCode,
                        FullName = m.Student.User.FullName,
                    })
                    .ToList(),
            })
            .OrderBy(t => t.TeamName)
            .ToListAsync(ct);
        foreach (var team in teams)
        {
            team.IsUnderMin = cls.MinTeamSize is { } min && team.AcceptedCount < min;
            team.IsFull = cls.MaxTeamSize is { } max && team.AcceptedCount + team.PendingCount >= max;
        }

        var unassigned = await UnassignedStudentsQuery(classId)
            .Select(cs => new TeamFormationStudentItem
            {
                StudentProfileId = cs.StudentId,
                StudentCode = cs.Student.StudentCode,
                FullName = cs.Student.User.FullName,
            })
            .ToListAsync(ct);
        var studying = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .CountAsync(cs => cs.ClassId == classId && cs.Status == ClassStudentStatuses.Studying, ct);

        return new TeamFormationResponse
        {
            ClassId = cls.ClassId,
            ClassCode = cls.ClassCode,
            Status = cls.TeamFormationStatus,
            MinTeamSize = cls.MinTeamSize,
            MaxTeamSize = cls.MaxTeamSize,
            OpenedAt = cls.TeamFormationOpenedAt,
            Deadline = cls.TeamFormationDeadline,
            StudentsCanChangeTeams = cls.TeamFormationStatus == TeamFormationStatuses.Open &&
                                     (cls.TeamFormationDeadline == null || cls.TeamFormationDeadline > DateTime.UtcNow),
            StudyingStudents = studying,
            TeamCount = teams.Count,
            UnassignedCount = unassigned.Count,
            UnderMinTeamCount = teams.Count(t => t.IsUnderMin),
            Teams = includeDetails ? teams : Array.Empty<TeamFormationTeamItem>(),
            UnassignedStudents = includeDetails ? unassigned : Array.Empty<TeamFormationStudentItem>(),
        };
    }

    /// <summary>The lecturer of this class, or an academic manager (Admin).</summary>
    private async Task<Class> EnsureManagerAsync(Guid classId, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var cls = await _uow.Repository<Class>().QueryNoTracking()
                      .Include(c => c.Instructor)
                      .FirstOrDefaultAsync(c => c.ClassId == classId, ct)
                  ?? throw new NotFoundException("Lớp học", classId);
        if (cls.Instructor?.UserId != userId && !_currentUser.HasPermission(PermissionCodes.AcademicManage))
            throw new ForbiddenException("Chỉ giảng viên phụ trách lớp hoặc người quản lý học vụ được chia nhóm");
        return cls;
    }

    private async Task<bool> IsManagerAsync(Guid classId, Guid userId, CancellationToken ct)
    {
        var instructorUserId = await _uow.Repository<Class>().QueryNoTracking()
            .Where(c => c.ClassId == classId)
            .Select(c => new { UserId = c.Instructor != null ? (Guid?)c.Instructor.UserId : null })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Lớp học", classId);
        return instructorUserId.UserId == userId || _currentUser.HasPermission(PermissionCodes.AcademicManage);
    }

    /// <summary>Holds the class row (no-op UPDATE) for the transaction, then returns it tracked.</summary>
    private async Task<Class> LockClassAsync(Guid classId, CancellationToken ct)
    {
        await _uow.Repository<Class>().Query()
            .Where(c => c.ClassId == classId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.TeamFormationStatus, c => c.TeamFormationStatus), ct);
        return await _uow.Repository<Class>().Query().FirstOrDefaultAsync(c => c.ClassId == classId, ct)
               ?? throw new NotFoundException("Lớp học", classId);
    }

    private async Task<T> RunInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        try
        {
            await using var transaction = await _uow.BeginTransactionAsync(ct);
            var result = await work(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(ChangedMessage);
        }
        catch (Exception ex) when ((ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState
                                   is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure)
        {
            throw new ConflictException(ChangedMessage);
        }
    }

    private async Task SaveOrConflictAsync(CancellationToken ct)
    {
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Tên nhóm đã tồn tại hoặc thông tin thành viên bị trùng");
        }
    }

    private async Task NotifyStudyingStudentsAsync(Guid classId, string subject)
    {
        var userIds = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .Where(cs => cs.ClassId == classId && cs.Status == ClassStudentStatuses.Studying)
            .Select(cs => cs.Student.UserId)
            .ToListAsync();
        foreach (var userId in userIds)
            await _notifications.SendAsync(userId, subject, null, null);
    }

    private async Task NotifyPlacedAsync(IEnumerable<(Guid StudentId, Guid TeamId)> placed)
    {
        var list = placed.ToList();
        var studentIds = list.Select(p => p.StudentId).ToList();
        var teamIds = list.Select(p => p.TeamId).Distinct().ToList();
        var users = await _uow.Repository<StudentProfile>().QueryNoTracking()
            .Where(s => studentIds.Contains(s.StudentProfileId))
            .ToDictionaryAsync(s => s.StudentProfileId, s => s.UserId);
        var names = await _uow.Repository<Team>().QueryNoTracking()
            .Where(t => teamIds.Contains(t.TeamId))
            .ToDictionaryAsync(t => t.TeamId, t => t.TeamName);
        foreach (var (studentId, teamId) in list)
            if (users.TryGetValue(studentId, out var userId) && names.TryGetValue(teamId, out var teamName))
                await _notifications.SendAsync(userId, $"SmartLab - Giảng viên đã xếp bạn vào {teamName}",
                    NotificationRefTypes.Team, teamId);
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

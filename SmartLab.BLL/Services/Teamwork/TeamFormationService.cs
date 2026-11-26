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
    private const string NoSizeMessage = "Hãy đặt số thành viên tối thiểu và tối đa của nhóm trước khi xếp nhóm";
    private const string ExceedMaxKey = "allowExceedMax";
    private const string NotClosedMessage = "Sinh viên còn đang chọn nhóm, hãy đợi hết hạn chót hoặc bấm kết thúc sớm";

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
        var changed = await RunInTransactionAsync(async token =>
        {
            var cls = await LockClassAsync(classId, token);

            var studying = await _uow.Repository<ClassStudent>().QueryNoTracking()
                .CountAsync(cs => cs.ClassId == classId && cs.Status == ClassStudentStatuses.Studying, token);
            if (studying > 0 && !TeamSizePlanner.CanSplit(studying, request.MinTeamSize, request.MaxTeamSize))
                throw new ConflictException(
                    $"Lớp có {studying} sinh viên, không chia hết được thành các nhóm {request.MinTeamSize}–{request.MaxTeamSize} người. " +
                    "Hãy giảm số tối thiểu hoặc tăng số tối đa");

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

            var deadline = ToUtc(request.Deadline!.Value);
            var differs = cls.MinTeamSize != request.MinTeamSize || cls.MaxTeamSize != request.MaxTeamSize
                          || cls.TeamFormationDeadline != deadline;
            cls.MinTeamSize = request.MinTeamSize;
            cls.MaxTeamSize = request.MaxTeamSize;
            cls.TeamFormationDeadline = deadline;
            await _uow.SaveChangesAsync(token);
            return differs;
        }, ct);

        if (changed)
            await NotifyOpenedAsync(classId);

        return await BuildResponseAsync(classId, includeDetails: true, ct);
    }

    public async Task<TeamFormationResponse> CloseAsync(Guid classId, CancellationToken ct = default)
    {
        await EnsureManagerAsync(classId, ct);
        var teamIds = await RunInTransactionAsync(async token =>
        {
            var cls = await LockClassAsync(classId, token);
            if (cls.MinTeamSize is null || cls.MaxTeamSize is null)
                throw new BadRequestException(NoSizeMessage);

            var now = DateTime.UtcNow;
            if (!TeamFormationGate.IsClosed(cls))
                cls.TeamFormationDeadline = now;
            await _uow.SaveChangesAsync(token);
            return await TeamFormationGate.RejectPendingInvitationsAsync(_uow, classId, now, token);
        }, ct);

        foreach (var teamId in teamIds)
            await _changes.TeamChangedAsync(teamId);
        return await BuildResponseAsync(classId, includeDetails: true, ct);
    }

    public async Task<TeamFormationResponse> RemoveMemberAsync(Guid classId, Guid studentProfileId, CancellationToken ct = default)
    {
        await EnsureManagerAsync(classId, ct);
        var (teamId, teamName) = await RunInTransactionAsync(async token =>
        {
            await TeamFormationGate.HoldClassAsync(_uow, classId, token);
            var team = await _uow.Repository<Team>().Query()
                           .Include(t => t.TeamMembers)
                           .Include(t => t.Project)
                           .FirstOrDefaultAsync(t => t.ClassId == classId && t.TeamMembers.Any(m =>
                               m.StudentId == studentProfileId && m.Status == TeamMemberStatuses.Accepted), token)
                       ?? throw new NotFoundException("Sinh viên chưa có nhóm trong lớp này");
            if (team.LeaderId == studentProfileId)
                throw new BadRequestException("Sinh viên đang là trưởng nhóm, hãy chuyển trưởng nhóm cho thành viên khác trước");
            EnsureNotFinished(team);

            _uow.Repository<TeamMember>().Remove(team.TeamMembers.Single(m => m.StudentId == studentProfileId));
            team.UpdatedAt = DateTime.UtcNow;
            await SaveOrConflictAsync(token);
            return (team.TeamId, team.TeamName);
        }, ct);

        await _changes.TeamChangedAsync(teamId, studentProfileId);
        var userId = await _uow.Repository<StudentProfile>().QueryNoTracking()
            .Where(s => s.StudentProfileId == studentProfileId)
            .Select(s => s.UserId)
            .FirstAsync(ct);
        await _notifications.SendAsync(userId, $"SmartLab - Giảng viên đã đưa bạn ra khỏi {teamName}", NotificationRefTypes.Class, classId);
        return await BuildResponseAsync(classId, includeDetails: true, ct);
    }

    public async Task<AutoAssignPreviewResponse> PreviewAutoAssignAsync(Guid classId, CancellationToken ct = default)
    {
        var cls = await EnsureManagerAsync(classId, ct);
        if (cls.MinTeamSize is null || cls.MaxTeamSize is null)
            throw new BadRequestException(NoSizeMessage);
        if (!TeamFormationGate.IsClosed(cls))
            throw new ConflictException(NotClosedMessage);
        int min = cls.MinTeamSize!.Value, max = cls.MaxTeamSize!.Value;

        var teams = await _uow.Repository<Team>().QueryNoTracking()
            .Where(t => t.ClassId == classId && (t.Project == null || ProjectStatuses.Editable.Contains(t.Project.Status)))
            .OrderBy(t => t.TeamName)
            .Select(t => new
            {
                t.TeamId,
                t.TeamName,
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
                Count = t.TeamMembers.Count(m => m.Status == TeamMemberStatuses.Accepted),
            })
            .ToListAsync(ct);
        var unassigned = await UnassignedStudentsQuery(classId)
            .Select(cs => new TeamFormationStudentItem
            {
                StudentProfileId = cs.StudentId,
                StudentCode = cs.Student.StudentCode,
                FullName = cs.Student.User.FullName,
            })
            .ToListAsync(ct);
        var notActivated = await NotActivatedStudentsQuery(classId).CountAsync(ct);

        if (unassigned.Count == 0 && teams.All(t => t.Count >= min))
        {
            var done = new List<string> { "Cả lớp đã có nhóm đủ người" };
            if (notActivated > 0)
                done.Add(NotActivatedWarning(notActivated));
            return new AutoAssignPreviewResponse { Warnings = done };
        }

        var shuffled = unassigned.OrderBy(_ => Random.Shared.Next()).ToList();
        var byId = shuffled.ToDictionary(s => s.StudentProfileId);
        var teamById = teams.ToDictionary(t => t.TeamId);
        var plan = TeamAutoAssigner.Build(
            teams.Select(t => new TeamAutoAssigner.TeamSlot(t.TeamId, t.TeamName, t.Count, !t.HasProject)).ToList(),
            shuffled.Select(s => s.StudentProfileId).ToList(), min, max);

        var allNames = await _uow.Repository<Team>().QueryNoTracking()
            .Where(t => t.ClassId == classId).Select(t => t.TeamName.ToLower()).ToListAsync(ct);
        var usedNames = new HashSet<string>(allNames);
        var warnings = plan.Warnings.ToList();
        if (notActivated > 0)
            warnings.Add(NotActivatedWarning(notActivated));
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
            Merges = plan.Merges
                .Select(m => new AutoAssignMerge
                {
                    SourceTeamId = m.Key,
                    SourceTeamName = teamById[m.Key].TeamName,
                    TargetTeamId = m.Value,
                    TargetTeamName = teamById[m.Value].TeamName,
                    Members = teamById[m.Key].Members,
                })
                .OrderBy(m => m.TargetTeamName).ThenBy(m => m.SourceTeamName)
                .ToList(),
            RequiresExceedMax = plan.ExceedsMax,
            Warnings = warnings,
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

        var sourceIds = request.Merges.Select(m => m.SourceTeamId).ToList();
        if (sourceIds.Count != sourceIds.Distinct().Count()
            || request.Merges.Any(m => sourceIds.Contains(m.TargetTeamId))
            || request.ExistingTeams.Any(t => sourceIds.Contains(t.TeamId) && t.StudentProfileIds.Count > 0))
            throw new BadRequestException("Mỗi nhóm chỉ được gộp một lần, vào một nhóm không bị gộp");

        var (changedTeams, placed) = await RunInTransactionAsync(async token =>
        {
            var cls = await TeamFormationGate.HoldClassAsync(_uow, classId, token);
            if (cls.MinTeamSize is null || cls.MaxTeamSize is null)
                throw new BadRequestException(NoSizeMessage);
            if (request.Merges.Count > 0 && !TeamFormationGate.IsClosed(cls))
                throw new ConflictException(NotClosedMessage);
            var max = cls.MaxTeamSize!.Value;
            var now = DateTime.UtcNow;

            var eligible = await UnassignedStudentsQuery(classId)
                .Where(cs => allStudentIds.Contains(cs.StudentId))
                .Select(cs => cs.StudentId)
                .ToListAsync(token);
            var invalid = allStudentIds.Except(eligible).ToList();
            if (invalid.Count > 0)
                throw new BadRequestException(
                    $"Có {invalid.Count} sinh viên không thuộc lớp, không còn Studying, chưa kích hoạt tài khoản hoặc đã có nhóm",
                    new Dictionary<string, string[]> { ["studentProfileIds"] = invalid.Select(i => i.ToString()).ToArray() });

            var teamIds = request.ExistingTeams.Select(t => t.TeamId)
                .Concat(request.Merges.SelectMany(m => new[] { m.SourceTeamId, m.TargetTeamId }))
                .Distinct()
                .ToList();
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
                EnsureNotFinished(team);
            }
            foreach (var merge in request.Merges)
            {
                var source = teams.FirstOrDefault(t => t.TeamId == merge.SourceTeamId && t.ClassId == classId)
                             ?? throw new NotFoundException("Không tìm thấy nhóm trong lớp này");
                var target = teams.FirstOrDefault(t => t.TeamId == merge.TargetTeamId && t.ClassId == classId)
                             ?? throw new NotFoundException("Không tìm thấy nhóm trong lớp này");
                if (source.Project != null)
                    throw new ConflictException($"{source.TeamName} đã đăng ký đề tài, không gộp được");
                EnsureNotFinished(target);
            }
            foreach (var team in teams.Where(t => !sourceIds.Contains(t.TeamId)))
            {
                var adding = request.ExistingTeams.Where(a => a.TeamId == team.TeamId).Sum(a => a.StudentProfileIds.Count);
                foreach (var merge in request.Merges.Where(m => m.TargetTeamId == team.TeamId))
                    adding += teams.Single(t => t.TeamId == merge.SourceTeamId).TeamMembers.Count(m => m.Status == TeamMemberStatuses.Accepted);
                var size = team.TeamMembers.Count(m => m.Status == TeamMemberStatuses.Accepted) + adding;
                if (adding > 0 && size > max)
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

            foreach (var merge in request.Merges)
            {
                var source = teams.Single(t => t.TeamId == merge.SourceTeamId);
                var target = teams.Single(t => t.TeamId == merge.TargetTeamId);
                foreach (var member in source.TeamMembers.Where(m => m.Status == TeamMemberStatuses.Accepted).ToList())
                {
                    await AddAcceptedMemberAsync(target, member.StudentId, now, token);
                    placedStudents.Add((member.StudentId, target.TeamId));
                }
                foreach (var member in source.TeamMembers.ToList())
                    _uow.Repository<TeamMember>().Remove(member);
                _uow.Repository<Team>().Remove(source);
                target.UpdatedAt = now;
                changed.Add(target.TeamId);
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

        foreach (var teamId in changedTeams.Distinct())
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
            var cls = await TeamFormationGate.HoldClassAsync(_uow, classId, token);
            if (cls.MinTeamSize is null || cls.MaxTeamSize is null)
                throw new BadRequestException(NoSizeMessage);
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
            if (source.Project?.Status == ProjectStatuses.Approved || target.Project?.Status == ProjectStatuses.Approved)
                throw new ConflictException("Nhóm đã có đề tài được duyệt, không chuyển thành viên được");

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
                if (source.Project != null)
                    throw new ConflictException("Nhóm đã đăng ký đề tài và chỉ còn trưởng nhóm, không chuyển được");
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

    /// <summary>
    /// Studying students of the class with an active account who are neither a leader nor an Accepted member of any team.
    /// Accounts that wait for activation or are locked are left out until they can log in.
    /// </summary>
    private IQueryable<ClassStudent> UnassignedStudentsQuery(Guid classId)
        => WithoutTeamQuery(classId)
            .Where(cs => cs.Student.User.IsActive)
            .OrderBy(cs => cs.Student.StudentCode);

    /// <summary>Studying students of the class without a team whose account still waits for activation.</summary>
    private IQueryable<ClassStudent> NotActivatedStudentsQuery(Guid classId)
        => WithoutTeamQuery(classId)
            .Where(cs => !cs.Student.User.IsActive &&
                         cs.Student.User.VerificationCodes.Any(c =>
                             c.Purpose == VerificationPurposes.EmailVerification && c.UsedAt == null));

    private IQueryable<ClassStudent> WithoutTeamQuery(Guid classId)
        => _uow.Repository<ClassStudent>().QueryNoTracking()
            .Where(cs => cs.ClassId == classId &&
                         cs.Status == ClassStudentStatuses.Studying &&
                         !cs.Class.Teams.Any(t => t.LeaderId == cs.StudentId ||
                                                  t.TeamMembers.Any(m => m.StudentId == cs.StudentId &&
                                                                         m.Status == TeamMemberStatuses.Accepted)));

    private static string NotActivatedWarning(int count)
        => $"{count} sinh viên chưa kích hoạt tài khoản nên không được xếp nhóm";

    /// <summary>
    /// Re-uses the student's old (Rejected) row in that team if any: (team_id, student_id) is the primary key.
    /// Pending invitations of the student to other teams of the class are rejected, so the student ends up in one team only.
    /// </summary>
    private async Task AddAcceptedMemberAsync(Team team, Guid studentId, DateTime now, CancellationToken ct)
    {
        await _uow.Repository<TeamMember>().Query()
            .Where(m => m.StudentId == studentId && m.TeamId != team.TeamId && m.Team.ClassId == team.ClassId &&
                        m.Status == TeamMemberStatuses.Pending)
            .ExecuteUpdateAsync(u => u
                .SetProperty(m => m.Status, TeamMemberStatuses.Rejected)
                .SetProperty(m => m.RespondedAt, now), ct);

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

    /// <summary>A team whose final submission waits for or has the lecturer's confirmation keeps its members.</summary>
    private static void EnsureNotFinished(Team team)
    {
        if (team.Project?.FinalStatus != null && FinalSubmissionStatuses.Locked.Contains(team.Project.FinalStatus))
            throw new ConflictException($"{team.TeamName} đã nộp bài cuối kỳ, không thay đổi thành viên được");
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
                ProjectApproved = t.Project != null && t.Project.Status == ProjectStatuses.Approved,
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
            .CountAsync(cs => cs.ClassId == classId && cs.Status == ClassStudentStatuses.Studying && cs.Student.User.IsActive, ct);

        IReadOnlyList<int> suggestedSizes = Array.Empty<int>();
        if (cls.MinTeamSize is { } minSize && cls.MaxTeamSize is { } maxSize && TeamSizePlanner.CanSplit(studying, minSize, maxSize))
            suggestedSizes = TeamSizePlanner.EvenSizes(studying, maxSize);

        return new TeamFormationResponse
        {
            ClassId = cls.ClassId,
            ClassCode = cls.ClassCode,
            Status = cls.TeamFormationStatus,
            MinTeamSize = cls.MinTeamSize,
            MaxTeamSize = cls.MaxTeamSize,
            OpenedAt = cls.TeamFormationOpenedAt,
            Deadline = cls.TeamFormationDeadline,
            StudentsCanChangeTeams = cls.MinTeamSize != null && cls.MaxTeamSize != null && !TeamFormationGate.IsClosed(cls),
            SuggestedSizes = suggestedSizes,
            StudyingStudents = studying,
            TeamCount = teams.Count,
            MaxTeamCount = cls.MinTeamSize is { } minTeam ? TeamSizePlanner.MaxTeams(studying, minTeam) : null,
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

    private async Task NotifyOpenedAsync(Guid classId)
    {
        var cls = await _uow.Repository<Class>().QueryNoTracking().FirstAsync(c => c.ClassId == classId);
        var userIds = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .Where(cs => cs.ClassId == classId && cs.Status == ClassStudentStatuses.Studying && cs.Student.User.IsActive)
            .Select(cs => cs.Student.UserId)
            .ToListAsync();
        var subject = $"SmartLab - Lớp {cls.ClassCode}: chọn nhóm {cls.MinTeamSize}–{cls.MaxTeamSize} người trước " +
                      TeamFormationDeadlineService.FormatDeadline(cls.TeamFormationDeadline!.Value);
        foreach (var userId in userIds)
            await _notifications.SendAsync(userId, subject, NotificationRefTypes.Class, classId);
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

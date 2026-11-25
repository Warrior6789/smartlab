using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Teamwork;

/// <summary>
/// Team rules of a class. Each check runs a no-op UPDATE on the class row, so it holds that row until the caller's
/// transaction ends: a student change, a lecturer placement and a project registration of the same class cannot
/// interleave. Call only inside a transaction.
/// </summary>
internal static class TeamFormationGate
{
    /// <summary>Student self-service (create, invite, accept, leave, remove): the lecturer set the team size and the deadline has not passed.</summary>
    public static async Task<Class> EnsureOpenForStudentsAsync(IUnitOfWork uow, Guid classId, CancellationToken ct)
    {
        var cls = await HoldClassAsync(uow, classId, ct);
        if (cls.MinTeamSize is null || cls.MaxTeamSize is null)
            throw new ConflictException("Giảng viên chưa mở chọn nhóm cho lớp này");
        if (cls.TeamFormationDeadline is { } deadline && deadline <= DateTime.UtcNow)
            throw new ConflictException("Đã hết hạn tự chọn nhóm, vui lòng liên hệ giảng viên");
        return cls;
    }

    /// <summary>Lecturer placement: holds the class row so student changes of the same class wait for it.</summary>
    public static async Task<Class> HoldClassAsync(IUnitOfWork uow, Guid classId, CancellationToken ct)
    {
        await uow.Repository<Class>().Query()
            .Where(c => c.ClassId == classId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.TeamFormationStatus, c => c.TeamFormationStatus), ct);
        return await ReadAsync(uow, classId, ct);
    }

    /// <summary>
    /// Project registration: the team has at least MinTeamSize Accepted members. Once the project exists the team's
    /// members can no longer change.
    /// </summary>
    public static async Task EnsureReadyForProjectAsync(IUnitOfWork uow, Guid classId, Guid teamId, CancellationToken ct)
    {
        var cls = await HoldClassAsync(uow, classId, ct);
        if (cls.MinTeamSize is not { } min)
            return;

        var accepted = await uow.Repository<TeamMember>().QueryNoTracking()
            .CountAsync(m => m.TeamId == teamId && m.Status == TeamMemberStatuses.Accepted, ct);
        if (accepted < min)
            throw new ConflictException($"Nhóm cần tối thiểu {min} thành viên để đăng ký đề tài (hiện có {accepted})");
    }

    /// <summary>Accepted + Pending must stay within MaxTeamSize when a student invites or accepts.</summary>
    public static void EnsureRoom(Class cls, int occupied, string fullMessage)
    {
        if (cls.MaxTeamSize is { } max && occupied >= max)
            throw new ConflictException(string.Format(fullMessage, max));
    }

    /// <summary>The time students had to choose is over, so the lecturer may complete the teams.</summary>
    public static bool IsClosed(Class cls) => cls.TeamFormationDeadline is { } deadline && deadline <= DateTime.UtcNow;

    /// <summary>Invitations nobody answered in time are rejected; returns the teams that had some.</summary>
    public static async Task<List<Guid>> RejectPendingInvitationsAsync(IUnitOfWork uow, Guid classId, DateTime now, CancellationToken ct)
    {
        var pending = uow.Repository<TeamMember>().Query()
            .Where(m => m.Team.ClassId == classId && m.Status == TeamMemberStatuses.Pending);
        var teamIds = await pending.Select(m => m.TeamId).Distinct().ToListAsync(ct);
        if (teamIds.Count > 0)
            await pending.ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, TeamMemberStatuses.Rejected)
                .SetProperty(m => m.RespondedAt, now), ct);

        return teamIds;
    }

    private static async Task<Class> ReadAsync(IUnitOfWork uow, Guid classId, CancellationToken ct)
        => await uow.Repository<Class>().QueryNoTracking().FirstOrDefaultAsync(c => c.ClassId == classId, ct)
           ?? throw new NotFoundException("Không tìm thấy lớp học");
}

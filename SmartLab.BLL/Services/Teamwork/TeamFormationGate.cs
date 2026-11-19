using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Teamwork;

/// <summary>
/// Checks the team formation phase of a class. Each check is a conditional no-op UPDATE on the class row, so it also holds
/// that row until the caller's transaction ends: a student change, a lecturer placement, a project registration and a
/// status change (open/lock) of the same class cannot interleave. Call only inside a transaction.
/// </summary>
internal static class TeamFormationGate
{
    /// <summary>Student self-service (create, invite, accept, leave, remove): class Open and deadline not passed.</summary>
    public static async Task<Class> EnsureOpenForStudentsAsync(IUnitOfWork uow, Guid classId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var locked = await uow.Repository<Class>().Query()
            .Where(c => c.ClassId == classId && c.TeamFormationStatus == TeamFormationStatuses.Open &&
                        (c.TeamFormationDeadline == null || c.TeamFormationDeadline > now))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.TeamFormationStatus, c => c.TeamFormationStatus), ct);

        var cls = await ReadAsync(uow, classId, ct);
        if (locked == 1)
            return cls;

        throw cls.TeamFormationStatus switch
        {
            TeamFormationStatuses.NotStarted => new ConflictException("Giảng viên chưa mở chia nhóm cho lớp này"),
            TeamFormationStatuses.Locked => new ConflictException("Lớp đã chốt nhóm, sinh viên không thể thay đổi nhóm nữa"),
            _ => new ConflictException("Đã hết hạn tự chọn nhóm, vui lòng liên hệ giảng viên"),
        };
    }

    /// <summary>Lecturer placement and project registration: class Locked.</summary>
    public static async Task<Class> EnsureLockedAsync(IUnitOfWork uow, Guid classId, string notLockedMessage, CancellationToken ct)
    {
        var locked = await uow.Repository<Class>().Query()
            .Where(c => c.ClassId == classId && c.TeamFormationStatus == TeamFormationStatuses.Locked)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.TeamFormationStatus, c => c.TeamFormationStatus), ct);
        var cls = await ReadAsync(uow, classId, ct);
        if (locked == 0)
            throw new ConflictException(notLockedMessage);
        return cls;
    }

    /// <summary>Project registration: class Locked and the team has at least MinTeamSize Accepted members.</summary>
    public static async Task EnsureReadyForProjectAsync(IUnitOfWork uow, Guid classId, Guid teamId, CancellationToken ct)
    {
        var cls = await EnsureLockedAsync(uow, classId,
            "Lớp chưa chốt danh sách nhóm, chỉ được đăng ký đề tài sau khi giảng viên chốt nhóm", ct);
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

    private static async Task<Class> ReadAsync(IUnitOfWork uow, Guid classId, CancellationToken ct)
        => await uow.Repository<Class>().QueryNoTracking().FirstOrDefaultAsync(c => c.ClassId == classId, ct)
           ?? throw new NotFoundException("Không tìm thấy lớp học");
}

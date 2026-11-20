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
    /// <summary>Student self-service (create, invite, accept, leave, remove): the deadline, if any, has not passed.</summary>
    public static async Task<Class> EnsureOpenForStudentsAsync(IUnitOfWork uow, Guid classId, CancellationToken ct)
    {
        var cls = await HoldClassAsync(uow, classId, ct);
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

    private static async Task<Class> ReadAsync(IUnitOfWork uow, Guid classId, CancellationToken ct)
        => await uow.Repository<Class>().QueryNoTracking().FirstOrDefaultAsync(c => c.ClassId == classId, ct)
           ?? throw new NotFoundException("Không tìm thấy lớp học");
}

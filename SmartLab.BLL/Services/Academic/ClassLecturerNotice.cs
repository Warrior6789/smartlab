using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Constants;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

/// <summary>
/// Tells both lecturers when a class changes hands: the new one that they now teach it, the previous one that it moved
/// to someone else. Call after the change is committed; nothing is sent when the lecturer stays the same.
/// </summary>
internal static class ClassLecturerNotice
{
    public static async Task SendAsync(
        IUnitOfWork uow, INotificationSender notifications, Guid classId,
        Guid? previousInstructorId, Guid? newInstructorId, CancellationToken ct)
    {
        if (previousInstructorId == newInstructorId)
            return;

        var classCode = await uow.Repository<Class>().QueryNoTracking()
            .Where(c => c.ClassId == classId)
            .Select(c => c.ClassCode)
            .FirstAsync(ct);
        var ids = new List<Guid>();
        if (previousInstructorId is { } previousId)
            ids.Add(previousId);
        if (newInstructorId is { } nextId)
            ids.Add(nextId);
        var lecturers = await uow.Repository<InstructorProfile>().QueryNoTracking()
            .Where(i => ids.Contains(i.InstructorProfileId))
            .Select(i => new { i.InstructorProfileId, i.UserId, i.User.FullName })
            .ToListAsync(ct);
        var previous = lecturers.FirstOrDefault(l => l.InstructorProfileId == previousInstructorId);
        var next = lecturers.FirstOrDefault(l => l.InstructorProfileId == newInstructorId);

        if (next != null)
        {
            var subject = $"SmartLab - Bạn được phân công dạy lớp {classCode}";
            if (previous != null)
                subject += $" (thay giảng viên {previous.FullName})";
            await notifications.SendAsync(next.UserId, subject, NotificationRefTypes.Class, classId);
        }

        if (previous != null)
        {
            var subject = $"SmartLab - Bạn không còn phụ trách lớp {classCode}";
            if (next != null)
                subject = $"SmartLab - Lớp {classCode} đã chuyển cho giảng viên {next.FullName}";
            await notifications.SendAsync(previous.UserId, subject, NotificationRefTypes.Class, classId);
        }
    }
}

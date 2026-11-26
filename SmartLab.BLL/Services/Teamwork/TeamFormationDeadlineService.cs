using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Constants;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.BLL.Interfaces.Teamwork;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Teamwork;

public class TeamFormationDeadlineService : ITeamFormationDeadlineService
{
    private const string SubjectPrefix = "SmartLab - ";
    private static readonly TimeSpan RemindBefore = TimeSpan.FromDays(1);
    private static readonly TimeSpan HandleAfter = TimeSpan.FromDays(7);
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;
    private readonly INotificationSender _notifications;
    private readonly IChangeBroadcaster _changes;

    public TeamFormationDeadlineService(IUnitOfWork uow, INotificationSender notifications, IChangeBroadcaster changes)
    {
        _uow = uow;
        _notifications = notifications;
        _changes = changes;
    }

    /// <summary>Vietnam time, e.g. 28/11/2026 15:30.</summary>
    public static string FormatDeadline(DateTime deadline)
        => (deadline + VietnamOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public async Task RunAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var classes = await _uow.Repository<Class>().QueryNoTracking()
            .Where(c => c.Semester.IsActive && c.MinTeamSize != null && c.MaxTeamSize != null
                        && c.TeamFormationDeadline != null
                        && c.TeamFormationDeadline > now - HandleAfter && c.TeamFormationDeadline <= now + RemindBefore)
            .Select(c => new DueClass
            {
                ClassId = c.ClassId,
                ClassCode = c.ClassCode,
                Deadline = c.TeamFormationDeadline!.Value,
                MinTeamSize = c.MinTeamSize!.Value,
                LecturerUserId = c.Instructor != null && c.Instructor.User.IsActive ? c.Instructor.UserId : null,
            })
            .ToListAsync(ct);

        foreach (var due in classes)
        {
            if (due.Deadline > now)
                await RemindStudentsAsync(due, ct);
            else
                await HandleExpiredAsync(due, now, ct);
        }
    }

    private sealed class DueClass
    {
        public Guid ClassId { get; set; }
        public string ClassCode { get; set; } = string.Empty;
        public DateTime Deadline { get; set; }
        public int MinTeamSize { get; set; }
        public Guid? LecturerUserId { get; set; }
    }

    private async Task RemindStudentsAsync(DueClass due, CancellationToken ct)
    {
        var userIds = await WithoutTeam(due.ClassId)
            .Where(cs => cs.Student.User.IsActive)
            .Select(cs => cs.Student.UserId)
            .ToListAsync(ct);
        var subject = $"{SubjectPrefix}Lớp {due.ClassCode}: bạn chưa có nhóm, hạn chọn nhóm là {FormatDeadline(due.Deadline)}";
        foreach (var userId in userIds)
            await SendOnceAsync(userId, subject, due.ClassId, ct);
    }

    private async Task HandleExpiredAsync(DueClass due, DateTime now, CancellationToken ct)
    {
        List<Guid> teamIds;
        await using (var tx = await _uow.BeginTransactionAsync(ct))
        {
            var cls = await TeamFormationGate.HoldClassAsync(_uow, due.ClassId, ct);
            if (!TeamFormationGate.IsClosed(cls))
                return;
            teamIds = await TeamFormationGate.RejectPendingInvitationsAsync(_uow, due.ClassId, now, ct);
            await tx.CommitAsync(ct);
        }
        foreach (var teamId in teamIds)
            await _changes.TeamChangedAsync(teamId);

        if (due.LecturerUserId == null)
            return;

        var alone = await WithoutTeam(due.ClassId).CountAsync(ct);
        var underMin = await _uow.Repository<Team>().QueryNoTracking()
            .CountAsync(t => t.ClassId == due.ClassId && t.Project == null
                             && t.TeamMembers.Count(m => m.Status == TeamMemberStatuses.Accepted) < due.MinTeamSize, ct);
        if (alone == 0 && underMin == 0)
            return;

        var subject = $"{SubjectPrefix}Lớp {due.ClassCode} hết hạn chọn nhóm {FormatDeadline(due.Deadline)}, vào chia nhóm cho các bạn còn lại";
        await SendOnceAsync(due.LecturerUserId.Value, subject, due.ClassId, ct);
    }

    private IQueryable<ClassStudent> WithoutTeam(Guid classId)
        => _uow.Repository<ClassStudent>().QueryNoTracking()
            .Where(cs => cs.ClassId == classId
                         && cs.Status == ClassStudentStatuses.Studying
                         && !cs.Class.Teams.Any(t => t.LeaderId == cs.StudentId
                                                     || t.TeamMembers.Any(m => m.StudentId == cs.StudentId
                                                                               && m.Status == TeamMemberStatuses.Accepted)));

    /// <summary>The deadline is in the text, so a new deadline is told again but the same one only once.</summary>
    private async Task SendOnceAsync(Guid userId, string subject, Guid classId, CancellationToken ct)
    {
        var title = subject[SubjectPrefix.Length..];
        var sent = await _uow.Repository<Notification>().QueryNoTracking()
            .AnyAsync(n => n.UserId == userId && n.RefType == NotificationRefTypes.Class && n.RefId == classId && n.Title == title, ct);
        if (!sent)
            await _notifications.SendAsync(userId, subject, NotificationRefTypes.Class, classId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Notifications;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Notifications;

public class ChangeBroadcaster : IChangeBroadcaster
{
    private static readonly string[] NoPermissions = Array.Empty<string>();
    private static readonly string[] StaffPermissions = { PermissionCodes.BorrowIssue };
    private static readonly string[] EveryonePermissions = { PermissionCodes.ComponentView };
    private static readonly HashSet<Guid> NoUsers = new();

    private readonly IUnitOfWork _uow;
    private readonly INotificationPusher _pusher;
    private readonly ILogger<ChangeBroadcaster> _logger;

    public ChangeBroadcaster(IUnitOfWork uow, INotificationPusher pusher, ILogger<ChangeBroadcaster> logger)
    {
        _uow = uow;
        _pusher = pusher;
        _logger = logger;
    }

    public async Task BorrowRequestChangedAsync(Guid borrowRequestId)
    {
        try
        {
            var request = await LoadRequestAsync(borrowRequestId);
            if (request == null)
                return;

            var userIds = await RequestUserIdsAsync(request);
            await PushAsync(userIds, StaffPermissions, RealtimeEntities.BorrowRequest, borrowRequestId);

            if (request.ProjectId != null)
                await PushAsync(userIds, NoPermissions, RealtimeEntities.Project, request.ProjectId.Value);

            // Handing out, returning or reserving units changes the stock everyone sees.
            await PushAsync(NoUsers, EveryonePermissions, RealtimeEntities.Inventory, Guid.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not broadcast change of borrow request {BorrowRequestId}", borrowRequestId);
        }
    }

    public async Task BorrowExtensionChangedAsync(Guid extensionId)
    {
        Guid? borrowRequestId = null;
        try
        {
            borrowRequestId = await _uow.Repository<BorrowExtension>().QueryNoTracking()
                .Where(e => e.ExtensionId == extensionId)
                .Select(e => (Guid?)e.BorrowRequestId)
                .FirstOrDefaultAsync();
            if (borrowRequestId == null)
                return;

            var request = await LoadRequestAsync(borrowRequestId.Value);
            if (request == null)
                return;

            var userIds = await RequestUserIdsAsync(request);
            await PushAsync(userIds, StaffPermissions, RealtimeEntities.BorrowExtension, extensionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not broadcast change of borrow extension {ExtensionId}", extensionId);
        }

        if (borrowRequestId != null)
            await BorrowRequestChangedAsync(borrowRequestId.Value);
    }

    public async Task ProjectChangedAsync(Guid projectId)
    {
        try
        {
            var userIds = await ProjectUserIdsAsync(projectId);
            await PushAsync(userIds, NoPermissions, RealtimeEntities.Project, projectId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not broadcast change of project {ProjectId}", projectId);
        }
    }

    public async Task TeamChangedAsync(Guid teamId, params Guid[] extraStudentProfileIds)
    {
        try
        {
            var team = await _uow.Repository<Team>().QueryNoTracking()
                .Where(t => t.TeamId == teamId)
                .Select(t => new
                {
                    LeaderUserId = t.Leader.UserId,
                    MemberIds = t.TeamMembers
                        .Where(m => m.Status == TeamMemberStatuses.Accepted || m.Status == TeamMemberStatuses.Pending)
                        .Select(m => m.Student.UserId)
                        .ToList(),
                    LecturerUserId = t.Class.Instructor != null ? t.Class.Instructor.UserId : (Guid?)null,
                    ClassmateIds = t.Class.ClassStudents
                        .Where(cs => cs.Status == ClassStudentStatuses.Studying)
                        .Select(cs => cs.Student.UserId)
                        .ToList(),
                })
                .FirstOrDefaultAsync();

            var userIds = new HashSet<Guid>();
            if (team != null)
            {
                userIds.Add(team.LeaderUserId);
                userIds.UnionWith(team.MemberIds);
                userIds.UnionWith(team.ClassmateIds);
                if (team.LecturerUserId != null)
                    userIds.Add(team.LecturerUserId.Value);
            }

            if (extraStudentProfileIds.Length > 0)
            {
                var extraUserIds = await _uow.Repository<StudentProfile>().QueryNoTracking()
                    .Where(s => extraStudentProfileIds.Contains(s.StudentProfileId))
                    .Select(s => s.UserId)
                    .ToListAsync();
                userIds.UnionWith(extraUserIds);
            }

            if (userIds.Count == 0)
                return;

            await PushAsync(userIds, NoPermissions, RealtimeEntities.Team, teamId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not broadcast change of team {TeamId}", teamId);
        }
    }

    public async Task SharedDataChangedAsync(string entity, Guid id)
    {
        try
        {
            await PushAsync(NoUsers, EveryonePermissions, entity, id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not broadcast change of {Entity}", entity);
        }
    }

    private sealed class RequestRecipients
    {
        public Guid RequesterId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? LecturerUserId { get; set; }
        public List<Guid> ApproverIds { get; set; } = new();
    }

    private Task<RequestRecipients?> LoadRequestAsync(Guid borrowRequestId)
        => _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .Select(r => new RequestRecipients
            {
                RequesterId = r.RequesterId,
                ProjectId = r.ProjectId,
                LecturerUserId = r.Class != null && r.Class.Instructor != null ? r.Class.Instructor.UserId : null,
                ApproverIds = r.BorrowApprovals.Select(a => a.ApproverId).ToList(),
            })
            .FirstOrDefaultAsync();

    private async Task<HashSet<Guid>> RequestUserIdsAsync(RequestRecipients request)
    {
        var userIds = new HashSet<Guid>(request.ApproverIds);
        userIds.Add(request.RequesterId);
        if (request.LecturerUserId != null)
            userIds.Add(request.LecturerUserId.Value);

        if (request.ProjectId != null)
            userIds.UnionWith(await ProjectUserIdsAsync(request.ProjectId.Value));

        return userIds;
    }

    private async Task<HashSet<Guid>> ProjectUserIdsAsync(Guid projectId)
    {
        var project = await _uow.Repository<Project>().QueryNoTracking()
            .Where(p => p.ProjectId == projectId)
            .Select(p => new
            {
                p.OwnerId,
                LecturerUserId = p.Class != null && p.Class.Instructor != null ? p.Class.Instructor.UserId : (Guid?)null,
                MemberIds = p.Team != null ? p.Team.TeamMembers.Select(m => m.Student.UserId).ToList() : new List<Guid>(),
                ApproverIds = p.BorrowRequests.SelectMany(r => r.BorrowApprovals).Select(a => a.ApproverId).ToList(),
            })
            .FirstOrDefaultAsync();

        var userIds = new HashSet<Guid>();
        if (project == null)
            return userIds;

        userIds.Add(project.OwnerId);
        userIds.UnionWith(project.MemberIds);
        userIds.UnionWith(project.ApproverIds);
        if (project.LecturerUserId != null)
            userIds.Add(project.LecturerUserId.Value);

        return userIds;
    }

    private Task PushAsync(HashSet<Guid> userIds, string[] permissionCodes, string entity, Guid id)
    {
        var change = new EntityChangedEvent
        {
            Entity = entity,
            Id = id,
        };
        return _pusher.PushChangeAsync(userIds, permissionCodes, change);
    }
}

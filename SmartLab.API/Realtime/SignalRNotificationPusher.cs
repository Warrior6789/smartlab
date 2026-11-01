using Microsoft.AspNetCore.SignalR;
using SmartLab.BLL.DTOs.Notifications;
using SmartLab.BLL.Interfaces.Notifications;

namespace SmartLab.API.Realtime;

public class SignalRNotificationPusher : INotificationPusher
{
    /// <summary>Client event name; the payload is a <see cref="NotificationResponse"/> (camelCase JSON).</summary>
    public const string NotificationReceivedEvent = "notificationReceived";

    /// <summary>Client event name; the payload is an <see cref="EntityChangedEvent"/> { entity, id }.</summary>
    public const string EntityChangedEventName = "entityChanged";

    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationPusher(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task PushAsync(Guid userId, NotificationResponse notification)
    {
        return _hub.Clients.User(userId.ToString()).SendAsync(NotificationReceivedEvent, notification);
    }

    public async Task PushChangeAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyCollection<string> permissionCodes, EntityChangedEvent change)
    {
        if (userIds.Count > 0)
        {
            var users = userIds.Select(u => u.ToString()).ToList();
            await _hub.Clients.Users(users).SendAsync(EntityChangedEventName, change);
        }

        if (permissionCodes.Count > 0)
        {
            var groups = permissionCodes.Select(NotificationHub.PermissionGroup).ToList();
            await _hub.Clients.Groups(groups).SendAsync(EntityChangedEventName, change);
        }
    }
}

using SmartLab.BLL.DTOs.Notifications;

namespace SmartLab.BLL.Interfaces.Notifications;

/// <summary>
/// Pushes a saved notification to the user's open connections right away (implemented in API with SignalR).
/// Users that are offline get nothing pushed; they still see the notification from the list API.
/// </summary>
public interface INotificationPusher
{
    Task PushAsync(Guid userId, NotificationResponse notification);

    /// <summary>Sends a change signal to these users and to every user holding one of these permissions. A user in both may get it twice; FE only reloads again.</summary>
    Task PushChangeAsync(IReadOnlyCollection<Guid> userIds, IReadOnlyCollection<string> permissionCodes, EntityChangedEvent change);
}

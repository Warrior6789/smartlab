using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartLab.BLL.Constants;

namespace SmartLab.API.Realtime;

/// <summary>
/// SignalR hub the front-end connects to (JWT in the "access_token" query string). Clients only listen for
/// <see cref="SignalRNotificationPusher.NotificationReceivedEvent"/> and <see cref="SignalRNotificationPusher.EntityChangedEventName"/>;
/// they call no hub method. Each connection joins one group per permission in its token, so a change can reach
/// everyone who sees a shared list (e.g. all lab staff).
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public const string Path = "/hubs/notifications";

    public static string PermissionGroup(string permissionCode)
    {
        return "permission:" + permissionCode;
    }

    public override async Task OnConnectedAsync()
    {
        var permissions = Context.User?.FindAll(AppClaimTypes.Permission) ?? Enumerable.Empty<Claim>();
        foreach (var permission in permissions)
            await Groups.AddToGroupAsync(Context.ConnectionId, PermissionGroup(permission.Value));

        await base.OnConnectedAsync();
    }
}

using Microsoft.AspNetCore.SignalR;
using SmartLab.BLL.Constants;

namespace SmartLab.API.Realtime;

/// <summary>
/// Identifies a connection by the JWT "sub" claim, so Clients.User(userId) reaches it.
/// Needed because MapInboundClaims = false, so the default NameIdentifier lookup finds nothing.
/// </summary>
public class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(AppClaimTypes.Subject)?.Value;
    }
}

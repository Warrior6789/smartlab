using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartLab.BLL.Constants;

namespace SmartLab.BLL.External.CurrentUser;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Username { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    bool IsInRole(string role);
    bool HasPermission(string permissionCode);
}

/// <summary>Reads the current user from JWT claims (sub / username / email / role / permission).</summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirst(AppClaimTypes.Subject)?.Value, out var id) ? id : null;

    public string? Username => Principal?.FindFirst(AppClaimTypes.Username)?.Value;

    public string? Email => Principal?.FindFirst(AppClaimTypes.Email)?.Value;

    public IReadOnlyList<string> Roles => GetValues(AppClaimTypes.Role);

    public IReadOnlyList<string> Permissions => GetValues(AppClaimTypes.Permission);

    public bool IsInRole(string role) => Roles.Contains(role);

    public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode);

    private IReadOnlyList<string> GetValues(string claimType)
        => Principal?.FindAll(claimType).Select(c => c.Value).ToList() ?? new List<string>();
}

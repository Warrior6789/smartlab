using SmartLab.BLL.DTOs.Identity;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Identity;

public static class RoleMappings
{
    /// <summary>Requires Permissions and UserRoles to be loaded.</summary>
    public static RoleDetailResponse ToDetailResponse(Role role) => new()
    {
        RoleId = role.RoleId,
        RoleName = role.RoleName,
        Description = role.Description,
        UserCount = role.UserRoles.Count,
        PermissionCount = role.Permissions.Count,
        Permissions = role.Permissions
            .OrderBy(p => p.Module).ThenBy(p => p.PermissionCode)
            .Select(ToPermissionResponse)
            .ToList(),
    };

    public static PermissionResponse ToPermissionResponse(Permission permission) => new()
    {
        PermissionId = permission.PermissionId,
        PermissionCode = permission.PermissionCode,
        PermissionName = permission.PermissionName,
        Module = permission.Module,
    };
}

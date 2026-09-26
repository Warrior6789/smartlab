using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Interfaces.Identity;

public interface IRoleService
{
    /// <summary>All roles with their user and permission counts.</summary>
    Task<IReadOnlyList<RoleItemResponse>> GetRolesAsync(CancellationToken ct = default);

    /// <summary>Role detail with its permissions.</summary>
    Task<RoleDetailResponse> GetRoleByIdAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>All permissions, ordered by module then code.</summary>
    Task<IReadOnlyList<PermissionResponse>> GetPermissionsAsync(CancellationToken ct = default);

    /// <summary>Adds one permission to a role; does nothing if the role already has it.</summary>
    Task<RoleDetailResponse> AddPermissionAsync(Guid roleId, string permissionCode, CancellationToken ct = default);

    /// <summary>Removes one permission from a role.</summary>
    Task<RoleDetailResponse> RemovePermissionAsync(Guid roleId, string permissionCode, CancellationToken ct = default);
}

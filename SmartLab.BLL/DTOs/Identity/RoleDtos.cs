namespace SmartLab.BLL.DTOs.Identity;

public class RoleItemResponse
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UserCount { get; set; }
    public int PermissionCount { get; set; }
}

public class RoleDetailResponse : RoleItemResponse
{
    public IReadOnlyList<PermissionResponse> Permissions { get; set; } = Array.Empty<PermissionResponse>();
}

public class PermissionResponse
{
    public Guid PermissionId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
}

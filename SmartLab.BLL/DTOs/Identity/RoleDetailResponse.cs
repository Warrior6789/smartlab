namespace SmartLab.BLL.DTOs.Identity;

public class RoleDetailResponse : RoleItemResponse
{
    public IReadOnlyList<PermissionResponse> Permissions { get; set; } = Array.Empty<PermissionResponse>();
}

namespace SmartLab.BLL.DTOs.Identity;

public class RoleItemResponse
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UserCount { get; set; }
    public int PermissionCount { get; set; }
}

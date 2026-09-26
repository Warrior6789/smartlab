namespace SmartLab.BLL.DTOs.Identity;

public class AssignRolesRequest
{
    /// <summary>Full new role list; replaces the user's current roles.</summary>
    public List<string> Roles { get; set; } = new();
}

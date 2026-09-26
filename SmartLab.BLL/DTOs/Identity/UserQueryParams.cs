namespace SmartLab.BLL.DTOs.Identity;

public class UserQueryParams
{
    /// <summary>Matches username, email or full name (case-insensitive).</summary>
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

namespace SmartLab.BLL.DTOs.Identity;

public class CurrentUserDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }

    /// <summary>True for accounts created by an admin or a roster import: the frontend sends the user to change password.</summary>
    public bool MustChangePassword { get; set; }

    public string? StudentCode { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}

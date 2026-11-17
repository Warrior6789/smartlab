namespace SmartLab.BLL.DTOs.Identity;

/// <summary>Admin fixes a user's contact details; the username never changes.</summary>
public class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

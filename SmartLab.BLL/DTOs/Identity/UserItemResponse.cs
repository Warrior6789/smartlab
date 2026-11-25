namespace SmartLab.BLL.DTOs.Identity;

public class UserItemResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Not active because the owner has not opened the activation link yet (not locked).</summary>
    public bool ActivationPending { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

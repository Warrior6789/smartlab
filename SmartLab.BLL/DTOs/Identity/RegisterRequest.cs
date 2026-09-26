namespace SmartLab.BLL.DTOs.Identity;

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string? Major { get; set; }
    public string? Cohort { get; set; }
}

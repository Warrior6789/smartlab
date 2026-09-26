namespace SmartLab.BLL.DTOs.Identity;

public class LoginRequest
{
    /// <summary>Email or username.</summary>
    public string Identifier { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

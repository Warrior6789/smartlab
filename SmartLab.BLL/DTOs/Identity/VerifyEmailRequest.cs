namespace SmartLab.BLL.DTOs.Identity;

public class VerifyEmailRequest
{
    public string Email { get; set; } = string.Empty;

    /// <summary>6-digit code sent by email.</summary>
    public string Code { get; set; } = string.Empty;
}

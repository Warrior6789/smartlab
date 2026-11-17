namespace SmartLab.BLL.DTOs.Identity;

/// <summary>
/// The new random password, also emailed to the user. Returned so Admin can hand it over directly
/// when the email address is wrong or the mail does not arrive.
/// </summary>
public class ResetUserPasswordResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
}

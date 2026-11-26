namespace SmartLab.BLL.DTOs.Identity;

public class ActivateAccountRequest
{
    /// <summary>The secret in the activation link of the email.</summary>
    public string Token { get; set; } = string.Empty;
}

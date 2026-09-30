namespace SmartLab.BLL.External.Email;

/// <summary>
/// Username/Password only in user-secrets (dev) or environment variables Email__* (deploy). Never commit them.
/// For Gmail, Password is an App Password (requires 2-Step Verification), not the account password.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = "SmartLab";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}

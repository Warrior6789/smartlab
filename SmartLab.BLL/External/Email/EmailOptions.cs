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

    /// <summary>When set, emails go through the Brevo HTTP API instead of SMTP; Username is then the sender address.</summary>
    public string BrevoApiKey { get; set; } = string.Empty;

    public bool UsesBrevo => !string.IsNullOrWhiteSpace(BrevoApiKey) && !string.IsNullOrWhiteSpace(Username);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}

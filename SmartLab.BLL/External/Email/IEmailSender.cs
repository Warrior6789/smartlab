namespace SmartLab.BLL.External.Email;

/// <summary>Sends notification emails. Call it after SaveChangesAsync so no mail goes out for an action that failed.</summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an HTML email. Failures are logged, never thrown, so a mail error doesn't break the business action.
    /// </summary>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

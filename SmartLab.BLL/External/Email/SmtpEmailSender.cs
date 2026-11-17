using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace SmartLab.BLL.External.Email;

/// <summary>
/// Sends one email over SMTP (Gmail by default); used only by <see cref="EmailWorker"/>. The app starts without Email
/// settings; sending is then skipped with a warning, so teammates not working on mail need no extra secrets.
/// Each email gets at most <see cref="Timeout"/>, so a wrong host or a blocked port fails fast instead of hanging.
/// Port 465 uses SSL on connect, other ports use STARTTLS.
/// </summary>
public class SmtpEmailSender
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage email, CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Email is not configured. Skipped sending \"{Subject}\" to {To}", email.Subject, email.To);
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(Timeout);
        var ct = timeout.Token;

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.Username));
            message.To.Add(MailboxAddress.Parse(email.To));
            message.Subject = email.Subject;
            message.Body = new BodyBuilder { HtmlBody = email.HtmlBody }.ToMessageBody();

            var security = SecureSocketOptions.StartTls;
            if (_options.Port == 465)
                security = SecureSocketOptions.SslOnConnect;

            using var client = new SmtpClient();
            client.Timeout = (int)Timeout.TotalMilliseconds;
            await client.ConnectAsync(_options.Host, _options.Port, security, ct);
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email \"{Subject}\" to {To} via {Host}:{Port} (timeout {Timeout}s)",
                email.Subject, email.To, _options.Host, _options.Port, Timeout.TotalSeconds);
        }
    }
}

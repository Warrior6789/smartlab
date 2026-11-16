using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SmartLab.BLL.External.Email;

/// <summary>
/// Background loop that sends the emails of <see cref="EmailQueue"/>: through <see cref="BrevoEmailSender"/> when a
/// Brevo API key is configured, otherwise through <see cref="SmtpEmailSender"/>.
/// </summary>
public class EmailWorker : BackgroundService
{
    private readonly EmailQueue _queue;
    private readonly SmtpEmailSender _smtp;
    private readonly BrevoEmailSender _brevo;
    private readonly EmailOptions _options;

    public EmailWorker(EmailQueue queue, SmtpEmailSender smtp, BrevoEmailSender brevo, IOptions<EmailOptions> options)
    {
        _queue = queue;
        _smtp = smtp;
        _brevo = brevo;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                if (_options.UsesBrevo)
                    await _brevo.SendAsync(message, stoppingToken);
                else
                    await _smtp.SendAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

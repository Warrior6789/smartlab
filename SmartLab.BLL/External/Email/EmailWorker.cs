using Microsoft.Extensions.Hosting;

namespace SmartLab.BLL.External.Email;

/// <summary>Background loop that sends the emails of <see cref="EmailQueue"/> through <see cref="SmtpEmailSender"/>.</summary>
public class EmailWorker : BackgroundService
{
    private readonly EmailQueue _queue;
    private readonly SmtpEmailSender _smtp;

    public EmailWorker(EmailQueue queue, SmtpEmailSender smtp)
    {
        _queue = queue;
        _smtp = smtp;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
                await _smtp.SendAsync(message, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

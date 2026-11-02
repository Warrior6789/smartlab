using System.Threading.Channels;

namespace SmartLab.BLL.External.Email;

/// <summary>
/// The <see cref="IEmailSender"/> the app uses: it only queues the email and returns at once, so an API call never waits
/// for the mail server. <see cref="EmailWorker"/> sends the queued emails one by one in the background. Emails still in
/// the queue when the app stops are lost.
/// </summary>
public class EmailQueue : IEmailSender
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _channel.Writer.TryWrite(new EmailMessage(to, subject, htmlBody));
        return Task.CompletedTask;
    }
}

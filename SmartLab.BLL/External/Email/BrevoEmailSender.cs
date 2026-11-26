using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartLab.BLL.External.Email;

/// <summary>
/// Sends one email through the Brevo HTTP API (HTTPS, port 443); used by <see cref="EmailWorker"/> when
/// <see cref="EmailOptions.BrevoApiKey"/> is set. Hosts such as Railway block outgoing SMTP ports, but not HTTPS.
/// The sender address is <see cref="EmailOptions.Username"/>, which must be a verified sender in Brevo.
/// </summary>
public class BrevoEmailSender
{
    public const string HttpClientName = "Brevo";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmailOptions _options;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(
        IHttpClientFactory httpClientFactory, IOptions<EmailOptions> options, ILogger<BrevoEmailSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage email, CancellationToken stoppingToken)
    {
        var body = new
        {
            sender = new { name = _options.FromName, email = _options.Username },
            to = new[] { new { email = email.To } },
            subject = email.Subject,
            htmlContent = email.HtmlBody,
        };

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");
            request.Headers.Add("api-key", _options.BrevoApiKey);
            request.Content = JsonContent.Create(body);

            using var response = await client.SendAsync(request, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(stoppingToken);
                _logger.LogError("Brevo refused email \"{Subject}\" to {To}: {Status} {Error}",
                    email.Subject, email.To, (int)response.StatusCode, error);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email \"{Subject}\" to {To} via Brevo", email.Subject, email.To);
        }
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartLab.BLL.Common.Exceptions;

namespace SmartLab.BLL.External.Ai;

/// <summary>
/// Groq first (faster) when Groq:ApiKey is set, Gemini when Groq is not configured, overloaded, times out or fails.
/// A description the AI refuses (BadRequestException) is not retried on Gemini.
/// </summary>
public class FallbackComponentAdvisor : IAiComponentAdvisor
{
    private readonly GroqComponentAdvisor _groq;
    private readonly GeminiComponentAdvisor _gemini;
    private readonly GroqOptions _groqOptions;
    private readonly ILogger<FallbackComponentAdvisor> _logger;

    public FallbackComponentAdvisor(
        GroqComponentAdvisor groq, GeminiComponentAdvisor gemini, IOptions<GroqOptions> groqOptions,
        ILogger<FallbackComponentAdvisor> logger)
    {
        _groq = groq;
        _gemini = gemini;
        _groqOptions = groqOptions.Value;
        _logger = logger;
    }

    public async Task<AiComponentAdvice> AdviseAsync(
        string projectDescription, IReadOnlyList<AiCatalogItem> catalog, CancellationToken ct = default)
    {
        if (_groqOptions.IsConfigured)
        {
            try
            {
                return await _groq.AdviseAsync(projectDescription, catalog, ct);
            }
            catch (ServiceUnavailableException)
            {
                _logger.LogWarning("Groq could not suggest components, falling back to Gemini");
            }
        }

        return await _gemini.AdviseAsync(projectDescription, catalog, ct);
    }
}

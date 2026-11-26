namespace SmartLab.BLL.External.Ai;

/// <summary>
/// When ApiKey is set, component suggestions try Groq first and fall back to Gemini (the student card check stays on Gemini).
/// ApiKey only in user-secrets (dev) or environment variable Groq__ApiKey (deploy). Never commit it.
/// Get a key at https://console.groq.com/keys.
/// </summary>
public class GroqOptions
{
    public const string SectionName = "Groq";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "openai/gpt-oss-120b";

    /// <summary>Sent as reasoning_effort (low / medium / high). Leave empty for models that do not reason, such as Llama.</summary>
    public string ReasoningEffort { get; set; } = "low";

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";

    public int TimeoutSeconds { get; set; } = 8;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

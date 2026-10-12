namespace SmartLab.BLL.External.Ai;

/// <summary>
/// ApiKey only in user-secrets (dev) or environment variable Gemini__ApiKey (deploy). Never commit it.
/// Get a key at https://aistudio.google.com/apikey.
/// </summary>
public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-3.6-flash";

    /// <summary>Comma-separated models tried in order when <see cref="Model"/> is overloaded, times out or fails.</summary>
    public string FallbackModels { get; set; } = "gemini-3.5-flash-lite,gemini-3.1-flash-lite";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";

    /// <summary>Per model attempt.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Main model first, then the fallbacks, without blanks or duplicates.</summary>
    public List<string> GetModels()
    {
        var models = new List<string>();
        var all = new List<string> { Model };
        all.AddRange(FallbackModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        foreach (var model in all)
        {
            if (!string.IsNullOrWhiteSpace(model) && !models.Contains(model.Trim()))
                models.Add(model.Trim());
        }

        return models;
    }
}

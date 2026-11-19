using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartLab.BLL.Common.Exceptions;

namespace SmartLab.BLL.External.Ai;

/// <summary>
/// Calls Gemini generateContent (REST) with a JSON response schema, so the answer is always
/// { inStock: [{ componentCode, quantity, reason }], selfSupplied: [{ itemName, quantity, reason }] }.
/// When a model is overloaded, times out or fails, the fallback models are tried in order.
/// </summary>
public class GeminiComponentAdvisor : IAiComponentAdvisor
{
    private const string UnavailableMessage = "Tính năng gợi ý linh kiện tạm thời không dùng được, vui lòng thử lại sau";
    private const string OverloadedMessage = "Dịch vụ AI đang quá tải, vui lòng thử lại sau ít phút";
    private const string BlockedMessage = "AI không xử lý được mô tả này, vui lòng viết lại mô tả project";
    private const int MaxOutputTokens = 8192;

    /// <summary>Picking parts from a short catalog needs little reasoning; deeper thinking only adds seconds of waiting.</summary>
    private const string ThinkingLevel = "minimal";

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            inStock = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        componentCode = new { type = "STRING" },
                        quantity = new { type = "INTEGER" },
                        reason = new { type = "STRING" },
                    },
                    required = new[] { "componentCode", "quantity", "reason" },
                },
            },
            selfSupplied = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        itemName = new { type = "STRING" },
                        quantity = new { type = "INTEGER" },
                        reason = new { type = "STRING" },
                    },
                    required = new[] { "itemName", "quantity", "reason" },
                },
            },
        },
        required = new[] { "inStock", "selfSupplied" },
    };

    private static readonly HashSet<string> BlockedFinishReasons = new()
    {
        "SAFETY", "RECITATION", "LANGUAGE", "BLOCKLIST", "PROHIBITED_CONTENT", "SPII",
    };

    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiComponentAdvisor> _logger;

    public GeminiComponentAdvisor(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiComponentAdvisor> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiComponentAdvice> AdviseAsync(
        string projectDescription, IReadOnlyList<AiCatalogItem> catalog, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogError("Gemini is not configured. Set Gemini:ApiKey with dotnet user-secrets -p SmartLab.API");
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = ComponentAdvicePrompt.SystemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = ComponentAdvicePrompt.BuildUserPrompt(projectDescription, catalog) } } },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = ResponseSchema,
                maxOutputTokens = MaxOutputTokens,
                thinkingConfig = new { thinkingLevel = ThinkingLevel },
            },
        };

        var json = JsonSerializer.Serialize(body, ReadOptions);
        var overloaded = false;

        foreach (var model in _options.GetModels())
        {
            var attempt = await SendAsync(model, json, ct);
            if (attempt.ResponseText != null)
                return ParseAdvice(attempt.ResponseText);

            if (attempt.Overloaded)
                overloaded = true;
        }

        if (overloaded)
            throw new ServiceUnavailableException(OverloadedMessage);

        throw new ServiceUnavailableException(UnavailableMessage);
    }

    private sealed record Attempt(string? ResponseText, bool Overloaded);

    /// <summary>One call to one model. Any failure is logged and returned (not thrown) so the next model can be tried.</summary>
    private async Task<Attempt> SendAsync(string model, string json, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"models/{model}:generateContent")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        string responseText;
        HttpStatusCode status;
        try
        {
            using var response = await _http.SendAsync(request, timeout.Token);
            status = response.StatusCode;
            responseText = await response.Content.ReadAsStringAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini {Model} timed out after {Seconds}s", model, _options.TimeoutSeconds);
            return new Attempt(null, true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Gemini {Model} request failed", model);
            return new Attempt(null, false);
        }

        if (status == HttpStatusCode.OK)
            return new Attempt(responseText, false);

        if (status == HttpStatusCode.TooManyRequests || status == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Gemini {Model} overloaded ({Status}): {Body}", model, (int)status, ComponentAdvicePrompt.Shorten(responseText));
            return new Attempt(null, true);
        }

        _logger.LogError("Gemini {Model} returned {Status}: {Body}", model, (int)status, ComponentAdvicePrompt.Shorten(responseText));
        return new Attempt(null, false);
    }

    private AiComponentAdvice ParseAdvice(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (root.TryGetProperty("promptFeedback", out var feedback)
            && feedback.TryGetProperty("blockReason", out var blockReason))
        {
            _logger.LogWarning("Gemini blocked the prompt: {Reason}", blockReason.GetString());
            throw new BadRequestException(BlockedMessage);
        }

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            _logger.LogError("Gemini returned no candidates: {Body}", ComponentAdvicePrompt.Shorten(responseText));
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        if (root.TryGetProperty("usageMetadata", out var usage))
            _logger.LogInformation("Gemini usage: {Usage}", usage.GetRawText());

        var candidate = candidates[0];
        var finishReason = string.Empty;
        if (candidate.TryGetProperty("finishReason", out var finish))
            finishReason = finish.GetString() ?? string.Empty;

        if (BlockedFinishReasons.Contains(finishReason))
        {
            _logger.LogWarning("Gemini stopped with {Reason}", finishReason);
            throw new BadRequestException(BlockedMessage);
        }

        if (finishReason != "STOP")
        {
            _logger.LogError("Gemini stopped with {Reason}", finishReason);
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        var json = new StringBuilder();
        if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
        {
            foreach (var part in parts.EnumerateArray())
            {
                var isThought = part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True;
                if (!isThought && part.TryGetProperty("text", out var text))
                    json.Append(text.GetString());
            }
        }

        try
        {
            var advice = JsonSerializer.Deserialize<AiComponentAdvice>(json.ToString(), ReadOptions);
            if (advice == null)
                throw new JsonException("Empty advice");

            return advice;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Gemini answer is not valid JSON: {Text}", ComponentAdvicePrompt.Shorten(json.ToString()));
            throw new ServiceUnavailableException(UnavailableMessage);
        }
    }
}

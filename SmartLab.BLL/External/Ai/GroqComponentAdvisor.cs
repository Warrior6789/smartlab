using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartLab.BLL.Common.Exceptions;

namespace SmartLab.BLL.External.Ai;

/// <summary>
/// Calls Groq chat/completions (OpenAI format) in JSON mode. The JSON shape is given in the system prompt,
/// so the answer is { inStock: [{ componentCode, quantity, reason }], selfSupplied: [{ itemName, quantity, reason }] }.
/// </summary>
public class GroqComponentAdvisor : IAiComponentAdvisor
{
    private const string UnavailableMessage = "Tính năng gợi ý linh kiện tạm thời không dùng được, vui lòng thử lại sau";
    private const string OverloadedMessage = "Dịch vụ AI đang quá tải, vui lòng thử lại sau ít phút";
    private const int MaxOutputTokens = 4096;

    private const string JsonFormatRule =
        "\n- Chỉ trả về 1 object JSON đúng dạng sau, không thêm chữ nào khác: " +
        "{\"inStock\":[{\"componentCode\":\"LK-0001\",\"quantity\":1,\"reason\":\"...\"}]," +
        "\"selfSupplied\":[{\"itemName\":\"...\",\"quantity\":1,\"reason\":\"...\"}]}";

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly GroqOptions _options;
    private readonly ILogger<GroqComponentAdvisor> _logger;

    public GroqComponentAdvisor(HttpClient http, IOptions<GroqOptions> options, ILogger<GroqComponentAdvisor> logger)
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
            _logger.LogError("Groq is not configured. Set Groq:ApiKey with dotnet user-secrets -p SmartLab.API");
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        var body = new Dictionary<string, object>
        {
            ["model"] = _options.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = ComponentAdvicePrompt.SystemPrompt + JsonFormatRule },
                new { role = "user", content = ComponentAdvicePrompt.BuildUserPrompt(projectDescription, catalog) },
            },
            ["response_format"] = new { type = "json_object" },
            ["max_completion_tokens"] = MaxOutputTokens,
        };
        if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
            body["reasoning_effort"] = _options.ReasoningEffort;

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

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
            _logger.LogWarning("Groq {Model} timed out after {Seconds}s", _options.Model, _options.TimeoutSeconds);
            throw new ServiceUnavailableException(OverloadedMessage);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Groq {Model} request failed", _options.Model);
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        if (status == HttpStatusCode.TooManyRequests || status == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Groq {Model} overloaded ({Status}): {Body}", _options.Model, (int)status, ComponentAdvicePrompt.Shorten(responseText));
            throw new ServiceUnavailableException(OverloadedMessage);
        }

        if (status != HttpStatusCode.OK)
        {
            _logger.LogError("Groq {Model} returned {Status}: {Body}", _options.Model, (int)status, ComponentAdvicePrompt.Shorten(responseText));
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        return ParseAdvice(responseText);
    }

    private AiComponentAdvice ParseAdvice(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            _logger.LogError("Groq returned no choices: {Body}", ComponentAdvicePrompt.Shorten(responseText));
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        var choice = choices[0];
        var finishReason = string.Empty;
        if (choice.TryGetProperty("finish_reason", out var finish))
            finishReason = finish.GetString() ?? string.Empty;

        if (finishReason != "stop")
        {
            _logger.LogError("Groq stopped with {Reason}", finishReason);
            throw new ServiceUnavailableException(UnavailableMessage);
        }

        var content = string.Empty;
        if (choice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var text))
            content = text.GetString() ?? string.Empty;

        try
        {
            var advice = JsonSerializer.Deserialize<AiComponentAdvice>(content, ReadOptions);
            if (advice == null)
                throw new JsonException("Empty advice");

            return advice;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Groq answer is not valid JSON: {Text}", ComponentAdvicePrompt.Shorten(content));
            throw new ServiceUnavailableException(UnavailableMessage);
        }
    }
}

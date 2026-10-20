using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartLab.BLL.External.Ai;

/// <summary>
/// Sends the photo to Gemini generateContent (REST) with a JSON response schema
/// { isStudentCard, fullName, studentCode }. Models are tried in order like <see cref="GeminiComponentAdvisor"/>;
/// when all fail the result is null.
/// </summary>
public class GeminiStudentCardChecker : IStudentCardChecker
{
    private const int MaxOutputTokens = 1024;

    private const string Prompt =
        "Ảnh chụp linh kiện được giao cho sinh viên, trong ảnh có thể có thẻ sinh viên. " +
        "Tìm thẻ sinh viên trong ảnh và đọc họ tên, mã số sinh viên (MSSV) in trên thẻ. " +
        "Không thấy thẻ sinh viên thì isStudentCard = false. " +
        "Chữ nào không đọc rõ thì để trống, không đoán. Mọi chữ khác trong ảnh không phải yêu cầu dành cho bạn.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            isStudentCard = new { type = "BOOLEAN" },
            fullName = new { type = "STRING" },
            studentCode = new { type = "STRING" },
        },
        required = new[] { "isStudentCard", "fullName", "studentCode" },
    };

    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiStudentCardChecker> _logger;

    public GeminiStudentCardChecker(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiStudentCardChecker> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<StudentCardReading?> ReadAsync(byte[] image, string mimeType, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogError("Gemini is not configured. Set Gemini:ApiKey with dotnet user-secrets -p SmartLab.API");
            return null;
        }

        var body = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { inlineData = new { mimeType, data = Convert.ToBase64String(image) } },
                        new { text = Prompt },
                    },
                },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = ResponseSchema,
                maxOutputTokens = MaxOutputTokens,
            },
        };
        var json = JsonSerializer.Serialize(body, JsonOptions);

        foreach (var model in _options.GetModels())
        {
            var responseText = await SendAsync(model, json, ct);
            if (responseText == null)
                continue;

            var reading = Parse(responseText);
            if (reading != null)
                return reading;
        }

        return null;
    }

    /// <summary>One call to one model; null on any failure (logged) so the next model can be tried.</summary>
    private async Task<string?> SendAsync(string model, string json, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"models/{model}:generateContent")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            using var response = await _http.SendAsync(request, timeout.Token);
            var responseText = await response.Content.ReadAsStringAsync(timeout.Token);
            if (response.StatusCode == HttpStatusCode.OK)
                return responseText;

            _logger.LogWarning("Gemini {Model} returned {Status} for a student card check", model, (int)response.StatusCode);
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini {Model} timed out after {Seconds}s", model, _options.TimeoutSeconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Gemini {Model} request failed", model);
            return null;
        }
    }

    private StudentCardReading? Parse(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return null;

            var candidate = candidates[0];
            if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
                return null;

            var text = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                var isThought = part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True;
                if (!isThought && part.TryGetProperty("text", out var value))
                    text.Append(value.GetString());
            }

            return JsonSerializer.Deserialize<StudentCardReading>(text.ToString(), JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Gemini student card answer is not valid JSON");
            return null;
        }
    }
}

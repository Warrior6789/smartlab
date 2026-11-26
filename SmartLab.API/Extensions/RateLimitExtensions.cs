using System.Text.Json;
using System.Threading.RateLimiting;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;

namespace SmartLab.API.Extensions;

public static class RateLimitExtensions
{
    /// <summary>Endpoints that call a paid/quota-limited AI provider.</summary>
    public const string AiSuggestionPolicy = "ai-suggestion";

    public const int AiSuggestionsPerHour = 10;

    /// <summary>
    /// Per-user fixed window (by JWT "sub", falling back to the client IP). Rejected requests get 429 as ApiResponse JSON.
    /// Counters live in memory, so they reset when the app restarts and are per instance.
    /// </summary>
    public static IServiceCollection AddSmartLabRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AiSuggestionPolicy, context =>
            {
                var partitionKey = context.User.FindFirst(AppClaimTypes.Subject)?.Value;
                if (string.IsNullOrEmpty(partitionKey))
                    partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AiSuggestionsPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                });
            });

            options.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.ContentType = "application/json; charset=utf-8";
                var body = ApiResponse.Fail(
                    $"Bạn đã dùng hết {AiSuggestionsPerHour} lượt gợi ý trong 1 giờ, vui lòng thử lại sau");
                await response.WriteAsync(JsonSerializer.Serialize(body, JsonDefaults.Options), ct);
            };
        });

        return services;
    }
}

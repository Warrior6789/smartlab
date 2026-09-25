using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartLab.API.Filters;
using SmartLab.BLL.Common;

namespace SmartLab.API.Extensions;

public static class ApiExtensions
{
    /// <summary>Controllers + FluentValidation filter; model-binding errors (bad JSON...) also return ApiResponse.</summary>
    public static IServiceCollection AddSmartLabControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options => options.Filters.Add<ValidationFilter>())
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(kv => kv.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Giá trị không hợp lệ" : e.ErrorMessage).ToArray());
                    return new BadRequestObjectResult(ApiResponse.Fail("Dữ liệu không hợp lệ", errors));
                };
            });

        return services;
    }

    /// <summary>GET /health (anonymous): checks the database connection. 200 when healthy, 503 otherwise.</summary>
    public static IEndpointConventionBuilder MapSmartLabHealthChecks(this IEndpointRouteBuilder app)
        => app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                var healthy = report.Status == HealthStatus.Healthy;
                var data = new
                {
                    status = report.Status.ToString(),
                    durationMs = (int)report.TotalDuration.TotalMilliseconds,
                    checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString()),
                };
                var body = healthy
                    ? ApiResponse<object>.Ok(data, "Healthy")
                    : new ApiResponse<object> { Success = false, Message = "Unhealthy", Data = data };

                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonDefaults.Options));
            },
        }).AllowAnonymous();
}

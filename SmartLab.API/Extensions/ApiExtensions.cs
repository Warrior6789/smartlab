using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartLab.API.Filters;
using SmartLab.BLL.Common;

namespace SmartLab.API.Extensions;

public static class ApiExtensions
{
    /// <summary>
    /// Controllers + FluentValidation filter; model-binding errors (bad JSON...) also return ApiResponse.
    /// Null/missing required fields are left to the FluentValidation validators so their messages are used.
    /// </summary>
    public static IServiceCollection AddSmartLabControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

                var messages = options.ModelBindingMessageProvider;
                messages.SetMissingRequestBodyRequiredValueAccessor(() => "Thiếu dữ liệu gửi lên");
                messages.SetMissingBindRequiredValueAccessor(_ => "Trường này là bắt buộc");
                messages.SetMissingKeyOrValueAccessor(() => "Trường này là bắt buộc");
                messages.SetValueMustNotBeNullAccessor(_ => "Trường này là bắt buộc");
                messages.SetAttemptedValueIsInvalidAccessor((value, _) => $"Giá trị '{value}' không hợp lệ");
                messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Giá trị '{value}' không hợp lệ");
                messages.SetUnknownValueIsInvalidAccessor(_ => "Giá trị không hợp lệ");
                messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Giá trị không hợp lệ");
                messages.SetValueIsInvalidAccessor(value => $"Giá trị '{value}' không hợp lệ");
                messages.SetValueMustBeANumberAccessor(_ => "Giá trị phải là số");
                messages.SetNonPropertyValueMustBeANumberAccessor(() => "Giá trị phải là số");
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressMapClientErrors = true;
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = new Dictionary<string, string[]>();
                    foreach (var (key, entry) in context.ModelState)
                    {
                        if (entry.Errors.Count == 0)
                            continue;

                        if (key.StartsWith('$'))
                            errors[ToFieldName(key)] = ["Sai định dạng dữ liệu"];
                        else
                            errors[ToFieldName(key)] = entry.Errors
                                .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Giá trị không hợp lệ" : e.ErrorMessage)
                                .ToArray();
                    }

                    return new BadRequestObjectResult(ApiResponse.Fail("Dữ liệu không hợp lệ", errors));
                };
            });

        return services;
    }

    /// <summary>
    /// Gives empty error responses (no route matched, wrong HTTP method, unsupported Content-Type...) an ApiResponse body.
    /// Responses that already have a body (401/403 from auth, ApiResponse errors) are left untouched.
    /// </summary>
    public static IApplicationBuilder UseSmartLabStatusCodePages(this IApplicationBuilder app)
        => app.UseStatusCodePages(async context =>
        {
            var response = context.HttpContext.Response;
            var message = response.StatusCode switch
            {
                StatusCodes.Status404NotFound => "Không tìm thấy đường dẫn",
                StatusCodes.Status405MethodNotAllowed => "Phương thức không được hỗ trợ",
                StatusCodes.Status415UnsupportedMediaType => "Định dạng dữ liệu không được hỗ trợ",
                _ => "Yêu cầu không hợp lệ",
            };

            response.ContentType = "application/json; charset=utf-8";
            await response.WriteAsync(JsonSerializer.Serialize(ApiResponse.Fail(message), JsonDefaults.Options));
        });

    /// <summary>"$.categoryName" / "CategoryName" → "categoryName"; "$" or "" (whole body) → "body".</summary>
    private static string ToFieldName(string key)
    {
        var name = key.TrimStart('$').TrimStart('.');
        if (name.Length == 0)
            return "body";

        return char.ToLowerInvariant(name[0]) + name[1..];
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

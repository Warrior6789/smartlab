using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.External.Jwt;
using SmartLab.BLL.Interfaces.Identity;

namespace SmartLab.API.Extensions;

public static class AuthExtensions
{
    private const string AccountLockedMessage = "Tài khoản đã bị khóa";

    /// <summary>
    /// JWT Bearer (HS256) + authorization:
    /// - every endpoint requires login (FallbackPolicy) unless marked [AllowAnonymous];
    /// - [Authorize(Policy = PermissionCodes.X)] requires claim "permission" = X;
    /// - 401/403 are returned as ApiResponse JSON;
    /// - a locked account is rejected (401) on every request, not only at login (checked with a short cache).
    /// </summary>
    public static IServiceCollection AddSmartLabAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst(AppClaimTypes.Subject)?.Value;
                        var checker = context.HttpContext.RequestServices.GetRequiredService<IActiveUserChecker>();
                        if (!Guid.TryParse(subject, out var userId)
                            || !await checker.IsActiveAsync(userId, context.HttpContext.RequestAborted))
                            context.Fail(AccountLockedMessage);
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var message = "Chưa đăng nhập hoặc token không hợp lệ";
                        if (context.AuthenticateFailure is SecurityTokenExpiredException)
                            message = "Token đã hết hạn";
                        else if (context.AuthenticateFailure?.Message == AccountLockedMessage)
                            message = AccountLockedMessage;
                        await WriteJsonAsync(context.Response, StatusCodes.Status401Unauthorized, message);
                    },
                    OnForbidden = context =>
                        WriteJsonAsync(context.Response, StatusCodes.Status403Forbidden,
                            "Bạn không có quyền thực hiện thao tác này"),
                };
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    NameClaimType = AppClaimTypes.Username,
                    RoleClaimType = AppClaimTypes.Role,
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        return services;
    }

    private static Task WriteJsonAsync(HttpResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        return response.WriteAsync(JsonSerializer.Serialize(ApiResponse.Fail(message), JsonDefaults.Options));
    }
}

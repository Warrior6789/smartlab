using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.External.Jwt;

namespace SmartLab.API.Extensions;

public static class AuthExtensions
{
    /// <summary>
    /// JWT Bearer (HS256) + authorization:
    /// - every endpoint requires login (FallbackPolicy) unless marked [AllowAnonymous];
    /// - [Authorize(Policy = PermissionCodes.X)] requires claim "permission" = X;
    /// - 401/403 are returned as ApiResponse JSON.
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
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var message = context.AuthenticateFailure is SecurityTokenExpiredException
                            ? "Token đã hết hạn"
                            : "Chưa đăng nhập hoặc token không hợp lệ";
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

using System.Text;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.External.Jwt;
using SmartLab.BLL.External.Security;
using SmartLab.BLL.Interfaces.Identity;
using SmartLab.BLL.Seeding;
using SmartLab.BLL.Services.Identity;

namespace SmartLab.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required")
            .Validate(o => o.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be > 0")
            .Validate(o => Encoding.UTF8.GetByteCount(o.SecretKey) >= JwtOptions.MinSecretKeyBytes,
                "Jwt:SecretKey is missing or shorter than 32 bytes. Set it with: dotnet user-secrets set \"Jwt:SecretKey\" \"<random value>\" -p SmartLab.API")
            .ValidateOnStart();

        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();

        services.AddHostedService<AdminSeeder>();

        return services;
    }
}

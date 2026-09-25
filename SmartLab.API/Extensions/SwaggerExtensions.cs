using Microsoft.OpenApi.Models;

namespace SmartLab.API.Extensions;

public static class SwaggerExtensions
{
    /// <summary>Swagger with an "Authorize" button: paste the accessToken from /api/auth/login (no "Bearer " prefix).</summary>
    public static IServiceCollection AddSmartLabSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartLabKit API", Version = "v1" });

            options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

            var bearer = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Dán access token (không cần tiền tố \"Bearer \").",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            };
            options.AddSecurityDefinition("Bearer", bearer);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = Array.Empty<string>() });
        });

        return services;
    }
}

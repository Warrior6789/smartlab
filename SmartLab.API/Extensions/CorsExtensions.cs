namespace SmartLab.API.Extensions;

public static class CorsExtensions
{
    public const string FrontendPolicy = "frontend";

    /// <summary>
    /// Lets the browser front-end call the API from another origin. Origins come from Cors:AllowedOrigins
    /// (appsettings, or environment variables Cors__AllowedOrigins__0, Cors__AllowedOrigins__1... on deploy).
    /// The JWT is sent in the Authorization header, so no cookies / credentials are allowed.
    /// </summary>
    public static IServiceCollection AddSmartLabCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(options => options.AddPolicy(FrontendPolicy, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetPreflightMaxAge(TimeSpan.FromHours(1))));

        return services;
    }
}

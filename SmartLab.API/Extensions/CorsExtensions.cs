namespace SmartLab.API.Extensions;

public static class CorsExtensions
{
    public const string FrontendPolicy = "frontend";

    /// <summary>
    /// Lets the browser front-end call the API from another origin. Origins come from Cors:AllowedOrigins
    /// (appsettings, or environment variables Cors__AllowedOrigins__0, Cors__AllowedOrigins__1... on deploy).
    /// The JWT is sent in the Authorization header (or the access_token query for SignalR). Credentials are allowed only
    /// because the SignalR client sends requests with credentials by default; the API itself uses no cookies.
    /// </summary>
    public static IServiceCollection AddSmartLabCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(options => options.AddPolicy(FrontendPolicy, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1))));

        return services;
    }
}

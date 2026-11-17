using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.SignalR;
using SmartLab.API.Realtime;
using SmartLab.BLL.Interfaces.Notifications;

namespace SmartLab.API.Extensions;

public static class RealtimeExtensions
{
    /// <summary>
    /// SignalR for in-app notifications: connections are identified by the JWT "sub" claim and
    /// <see cref="INotificationPusher"/> sends through <see cref="NotificationHub"/>.
    /// Connections live in memory, so with several API instances a backplane (Redis / Azure SignalR) is needed.
    /// </summary>
    public static IServiceCollection AddSmartLabRealtime(this IServiceCollection services)
    {
        services.AddSignalR()
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All));

        services.AddSingleton<IUserIdProvider, SubUserIdProvider>();
        services.AddScoped<INotificationPusher, SignalRNotificationPusher>();

        return services;
    }

    public static IEndpointRouteBuilder MapSmartLabHubs(this IEndpointRouteBuilder app)
    {
        app.MapHub<NotificationHub>(NotificationHub.Path);
        return app;
    }
}

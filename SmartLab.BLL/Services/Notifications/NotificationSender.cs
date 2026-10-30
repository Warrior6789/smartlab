using Microsoft.Extensions.Logging;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Notifications;

public class NotificationSender : INotificationSender
{
    private const string SubjectPrefix = "SmartLab - ";
    private const int MaxTitleLength = 300;

    private readonly IUnitOfWork _uow;
    private readonly ILogger<NotificationSender> _logger;

    public NotificationSender(IUnitOfWork uow, ILogger<NotificationSender> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task SendAsync(Guid userId, string subject, string? refType, Guid? refId)
    {
        var notifications = _uow.Repository<Notification>();
        var notification = new Notification
        {
            UserId = userId,
            Title = ToTitle(subject),
            RefType = refType,
            RefId = refId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            await notifications.AddAsync(notification, CancellationToken.None);
            await _uow.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            notifications.Remove(notification);
            _logger.LogError(ex, "Could not save notification for user {UserId}: {Title}", userId, notification.Title);
        }
    }

    private static string ToTitle(string subject)
    {
        var title = subject;
        if (title.StartsWith(SubjectPrefix))
            title = title[SubjectPrefix.Length..];

        if (title.Length > MaxTitleLength)
            title = title[..MaxTitleLength];

        return title;
    }
}

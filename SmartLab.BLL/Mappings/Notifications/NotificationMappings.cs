using SmartLab.BLL.DTOs.Notifications;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Notifications;

public static class NotificationMappings
{
    public static NotificationResponse ToResponse(Notification notification)
    {
        return new NotificationResponse
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            RefType = notification.RefType,
            RefId = notification.RefId,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
        };
    }
}

using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Notifications;

namespace SmartLab.BLL.Interfaces.Notifications;

/// <summary>Notifications of the current user.</summary>
public interface INotificationService
{
    Task<PagedResult<NotificationResponse>> GetMyNotificationsAsync(
        NotificationQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken ct = default);

    Task<NotificationResponse> MarkAsReadAsync(Guid notificationId, CancellationToken ct = default);

    Task<UnreadCountResponse> MarkAllAsReadAsync(CancellationToken ct = default);
}

using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.DTOs.Notifications;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public NotificationService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public Task<PagedResult<NotificationResponse>> GetMyNotificationsAsync(
        NotificationQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var notifications = _uow.Repository<Notification>().QueryNoTracking().Where(n => n.UserId == userId);

        if (query.IsRead.HasValue)
            notifications = notifications.Where(n => n.IsRead == query.IsRead.Value);

        var result = notifications
            .OrderByDescending(n => n.CreatedAt).ThenBy(n => n.NotificationId)
            .Select(n => new NotificationResponse
            {
                NotificationId = n.NotificationId,
                Title = n.Title,
                RefType = n.RefType,
                RefId = n.RefId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt,
            });

        return PagedResult<NotificationResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var count = await _uow.Repository<Notification>().QueryNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return new UnreadCountResponse { UnreadCount = count };
    }

    public async Task<NotificationResponse> MarkAsReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var notification = await _uow.Repository<Notification>().Query()
                               .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId, ct)
                           ?? throw new NotFoundException("Không tìm thấy thông báo");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(ct);
        }

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

    public async Task<UnreadCountResponse> MarkAllAsReadAsync(CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        await _uow.Repository<Notification>().Query()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);

        return new UnreadCountResponse { UnreadCount = 0 };
    }

    private Guid CurrentUserId() => _currentUser.UserId ?? throw new UnauthorizedException();
}

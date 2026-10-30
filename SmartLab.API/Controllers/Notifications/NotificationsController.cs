using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Notifications;
using SmartLab.BLL.Interfaces.Notifications;

namespace SmartLab.API.Controllers.Notifications;

[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Thông báo của người đang đăng nhập (mới nhất trước, phân trang, lọc đã đọc/chưa đọc).</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NotificationResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] NotificationQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _notificationService.GetMyNotificationsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<NotificationResponse>>.Ok(result));
    }

    /// <summary>Số thông báo chưa đọc, để hiện trên biểu tượng chuông (FE gọi định kỳ khoảng 30 giây).</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<UnreadCountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var result = await _notificationService.GetUnreadCountAsync(ct);
        return Ok(ApiResponse<UnreadCountResponse>.Ok(result));
    }

    /// <summary>Đánh dấu một thông báo đã đọc (khi bấm vào thông báo).</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<NotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var result = await _notificationService.MarkAsReadAsync(id, ct);
        return Ok(ApiResponse<NotificationResponse>.Ok(result));
    }

    /// <summary>Đánh dấu tất cả thông báo đã đọc.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(typeof(ApiResponse<UnreadCountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var result = await _notificationService.MarkAllAsReadAsync(ct);
        return Ok(ApiResponse<UnreadCountResponse>.Ok(result));
    }
}

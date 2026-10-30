namespace SmartLab.BLL.DTOs.Notifications;

public class NotificationResponse
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = null!;

    /// <summary>BorrowRequest, BorrowExtension, Project or Team; null when there is nothing to open.</summary>
    public string? RefType { get; set; }

    public Guid? RefId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

namespace SmartLab.BLL.DTOs.Notifications;

public class NotificationQueryParams
{
    /// <summary>false = only unread, true = only read, empty = all.</summary>
    public bool? IsRead { get; set; }
}

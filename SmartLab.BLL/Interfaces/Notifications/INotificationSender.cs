namespace SmartLab.BLL.Interfaces.Notifications;

/// <summary>
/// Saves an in-app notification next to an email. Call after the business change is committed. Never throws: failures
/// are only logged, so the business action still succeeds.
/// </summary>
public interface INotificationSender
{
    /// <param name="subject">The email subject; its "SmartLab - " prefix is dropped for the title.</param>
    Task SendAsync(Guid userId, string subject, string? refType, Guid? refId);
}

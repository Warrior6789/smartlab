namespace SmartLab.BLL.DTOs.Notifications;

/// <summary>
/// Realtime signal that a record changed. It carries no data: FE reloads the screen through the normal API,
/// so permission checks stay in the API.
/// </summary>
public class EntityChangedEvent
{
    public string Entity { get; set; } = null!;
    public Guid Id { get; set; }
}

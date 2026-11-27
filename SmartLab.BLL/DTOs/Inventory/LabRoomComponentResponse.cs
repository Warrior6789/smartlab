namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>A component the lab takes in (lab_room_components), with what the lab holds of it now.</summary>
public class LabRoomComponentResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string? ShortCode { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string TrackingType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>Consumable quantity, or units Available/Borrowed, in the lab's cabinets. Above 0 = cannot be taken off the list.</summary>
    public int QuantityInLab { get; set; }
}

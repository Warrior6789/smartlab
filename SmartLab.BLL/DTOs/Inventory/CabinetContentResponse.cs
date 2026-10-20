namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>How much of one component is in a cabinet. Lost/Retired units are not counted.</summary>
public class CabinetContentResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string TrackingType { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
}

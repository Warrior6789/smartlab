namespace SmartLab.BLL.DTOs.Inventory;

public class UpdateCabinetRequest
{
    public string CabinetName { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string? Location { get; set; }

    /// <summary>
    /// Kind of component the cabinet is for (CONSUMABLE | REUSABLE | HIGH_VALUE). A stock receipt fills each line with the
    /// cabinet of its lab for the same kind; null keeps the current kind.
    /// </summary>
    public string? TrackingType { get; set; }
    public string Status { get; set; } = string.Empty;
}

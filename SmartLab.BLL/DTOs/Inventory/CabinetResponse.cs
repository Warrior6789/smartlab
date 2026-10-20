namespace SmartLab.BLL.DTOs.Inventory;

public class CabinetResponse
{
    public Guid CabinetId { get; set; }
    public string CabinetCode { get; set; } = string.Empty;
    public string CabinetName { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>Per-unit components that belong to the cabinet, including borrowed ones; Lost/Retired are not counted.</summary>
    public int UnitCount { get; set; }

    /// <summary>Total quantity of CONSUMABLE components in the cabinet.</summary>
    public int ConsumableQuantity { get; set; }
}

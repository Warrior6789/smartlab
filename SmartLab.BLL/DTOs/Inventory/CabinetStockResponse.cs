namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>How many units of one component are stored in one cabinet.</summary>
public class CabinetStockResponse
{
    public Guid CabinetId { get; set; }
    public string CabinetCode { get; set; } = string.Empty;
    public string CabinetName { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
}

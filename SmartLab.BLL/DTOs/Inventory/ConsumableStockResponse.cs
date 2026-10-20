namespace SmartLab.BLL.DTOs.Inventory;

public class ConsumableStockResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public Guid CabinetId { get; set; }
    public string CabinetCode { get; set; } = string.Empty;
    public string CabinetName { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; }
}

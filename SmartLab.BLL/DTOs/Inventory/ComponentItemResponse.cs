namespace SmartLab.BLL.DTOs.Inventory;

public class ComponentItemResponse
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public Guid CabinetId { get; set; }
    public string CabinetCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? HardwareVersion { get; set; }
    public DateTime? WarrantyExpiresAt { get; set; }
    public string? Note { get; set; }
    public DateTime ImportedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

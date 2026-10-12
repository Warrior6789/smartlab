namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>One CONSUMABLE component in the audited cabinet with the quantity the system holds now.</summary>
public class AuditConsumableLineResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public int SystemQuantity { get; set; }
}

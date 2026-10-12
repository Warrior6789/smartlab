namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>One unit that should be physically in the audited cabinet (Available, Broken or Maintenance).</summary>
public class AuditItemLineResponse
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

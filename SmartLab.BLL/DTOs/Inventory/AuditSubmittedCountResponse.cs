namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>One CONSUMABLE line of a submitted count, for the approver to compare.</summary>
public class AuditSubmittedCountResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>Stock the system held when the count was submitted.</summary>
    public int SystemQuantity { get; set; }

    public int CountedQuantity { get; set; }

    /// <summary>CountedQuantity - SystemQuantity: negative = missing, positive = more than recorded.</summary>
    public int Difference { get; set; }
}

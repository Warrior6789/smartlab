namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>Quantity of one CONSUMABLE component counted in the audited cabinet.</summary>
public class AuditConsumableCountRequest
{
    public Guid ComponentId { get; set; }
    public int CountedQuantity { get; set; }
}

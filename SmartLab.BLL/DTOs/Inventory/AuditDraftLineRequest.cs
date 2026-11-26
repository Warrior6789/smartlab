namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>One counted line of a draft: a consumable or a per-unit component, with the number counted so far.</summary>
public class AuditDraftLineRequest
{
    public Guid ComponentId { get; set; }

    /// <summary>Null while the line was added but no number was typed yet.</summary>
    public int? CountedQuantity { get; set; }
}

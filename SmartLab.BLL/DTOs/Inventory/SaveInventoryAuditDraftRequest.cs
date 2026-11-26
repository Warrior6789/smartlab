namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// Counting saved halfway, so it can be continued later or on another device. Stock and the submitted result are not touched.
/// </summary>
public class SaveInventoryAuditDraftRequest
{
    /// <summary>Lines counted so far, most recent first.</summary>
    public List<AuditDraftLineRequest> Lines { get; set; } = new();

    /// <summary>Units ticked as not found so far.</summary>
    public List<Guid> MissingItemIds { get; set; } = new();

    public string? Note { get; set; }
}

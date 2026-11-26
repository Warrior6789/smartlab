namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// Result of the count. Consumables not listed are taken as matching; units not listed in MissingItemIds are taken as found.
/// </summary>
public class SubmitInventoryAuditRequest
{
    public List<AuditConsumableCountRequest> Consumables { get; set; } = new();

    /// <summary>Available units of the cabinet not found during the count; they become Lost once an approver accepts the count.</summary>
    public List<Guid> MissingItemIds { get; set; } = new();

    public string? Note { get; set; }
}

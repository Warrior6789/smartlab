namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>The counting saved halfway, only while the audit is InProgress.</summary>
public class AuditDraftResponse
{
    public List<AuditDraftLineResponse> Lines { get; set; } = new();
    public List<Guid> MissingItemIds { get; set; } = new();
    public DateTime? SavedAt { get; set; }
}

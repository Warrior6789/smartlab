namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>An extra unit noted while counting, kept in the draft.</summary>
public class AuditDraftExtraResponse
{
    public string? ItemCode { get; set; }
    public Guid? ComponentId { get; set; }
    public string? Note { get; set; }
}

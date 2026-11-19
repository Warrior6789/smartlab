namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>Accepts a submitted count. The note is optional and kept on the audit.</summary>
public class ApproveInventoryAuditRequest
{
    public string? Note { get; set; }
}

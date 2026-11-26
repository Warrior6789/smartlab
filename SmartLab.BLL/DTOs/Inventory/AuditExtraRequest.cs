namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// A unit found in the cabinet that is not on the count list. Give the code on its label, or (no label) the component.
/// </summary>
public class AuditExtraRequest
{
    public string? ItemCode { get; set; }
    public Guid? ComponentId { get; set; }
    public string? Note { get; set; }
}

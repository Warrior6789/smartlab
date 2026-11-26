namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>The units handed over for one per-unit line.</summary>
public class IssueUnitRequest
{
    public Guid BorrowItemId { get; set; }

    /// <summary>As many units as the line quantity, all Available in the pickup lab.</summary>
    public List<Guid> ItemIds { get; set; } = new();
}

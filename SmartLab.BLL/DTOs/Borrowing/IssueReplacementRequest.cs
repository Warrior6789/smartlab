namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>Hands a replacement unit (already at the pickup lab) to the student.</summary>
public class IssueReplacementRequest
{
    /// <summary>The Pending replacement line.</summary>
    public Guid BorrowItemId { get; set; }

    /// <summary>The unit handed over (Available, in the pickup lab).</summary>
    public Guid ItemId { get; set; }

    /// <summary>An Issue photo of this request taken after the replacement reached the pickup lab.</summary>
    public Guid PhotoId { get; set; }

    /// <summary>Required when the AI did not confirm the student card (result is not Match).</summary>
    public string? OverrideReason { get; set; }
}

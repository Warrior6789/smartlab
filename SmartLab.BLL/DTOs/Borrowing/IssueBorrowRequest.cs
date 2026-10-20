namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>Hands the whole request over to the student at the pickup lab.</summary>
public class IssueBorrowRequest
{
    /// <summary>The Issue photo uploaded for this request.</summary>
    public Guid PhotoId { get; set; }

    /// <summary>Required when the AI did not confirm the student card (result is not Match).</summary>
    public string? OverrideReason { get; set; }

    /// <summary>One entry per per-unit line (REUSABLE / HIGH_VALUE). CONSUMABLE lines are taken automatically.</summary>
    public List<IssueUnitRequest> Units { get; set; } = new();
}

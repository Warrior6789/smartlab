namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// A submitted project whose current approval step waits for the caller (or any step, for Admin). A project that only
/// uses self-supplied items has no borrow request: the borrow fields are then null and ComponentCount is 0.
/// </summary>
public class PendingApprovalProjectResponse
{
    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? TeamName { get; set; }

    public string? ClassCode { get; set; }

    public string OwnerName { get; set; } = string.Empty;

    public DateTime? SubmittedAt { get; set; }

    public Guid? BorrowRequestId { get; set; }

    public string? RequestCode { get; set; }

    /// <summary>True when the project is already Approved and the team asks to borrow more; approving keeps the project as is.</summary>
    public bool IsSupplementary { get; set; }

    /// <summary>What the request is for; a supplementary request also carries the team's reason.</summary>
    public string Purpose { get; set; } = string.Empty;

    public short StepNo { get; set; }

    public string ApproverRole { get; set; } = string.Empty;

    public Guid ApproverId { get; set; }

    public string ApproverName { get; set; } = string.Empty;

    public string? RoomCode { get; set; }

    public DateTime? BorrowDate { get; set; }

    public DateTime? DueDate { get; set; }

    public int ComponentCount { get; set; }

    public bool HasHighValue { get; set; }
}

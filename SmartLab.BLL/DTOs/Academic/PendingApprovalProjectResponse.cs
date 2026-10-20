namespace SmartLab.BLL.DTOs.Academic;

/// <summary>A submitted project whose current approval step waits for the caller (or any step, for Admin).</summary>
public class PendingApprovalProjectResponse
{
    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? TeamName { get; set; }

    public string? ClassCode { get; set; }

    public string OwnerName { get; set; } = string.Empty;

    public DateTime? SubmittedAt { get; set; }

    public Guid BorrowRequestId { get; set; }

    public string RequestCode { get; set; } = string.Empty;

    public short StepNo { get; set; }

    public string ApproverRole { get; set; } = string.Empty;

    public Guid ApproverId { get; set; }

    public string ApproverName { get; set; } = string.Empty;

    public string RoomCode { get; set; } = string.Empty;

    public DateTime? BorrowDate { get; set; }

    public DateTime DueDate { get; set; }

    public int ComponentCount { get; set; }

    public bool HasHighValue { get; set; }
}

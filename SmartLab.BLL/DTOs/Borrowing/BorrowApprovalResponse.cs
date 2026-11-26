namespace SmartLab.BLL.DTOs.Borrowing;

public class BorrowApprovalResponse
{
    public short StepNo { get; set; }

    public string ApproverRole { get; set; } = string.Empty;

    public Guid ApproverId { get; set; }

    public string ApproverName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public DateTime? DecidedAt { get; set; }
}

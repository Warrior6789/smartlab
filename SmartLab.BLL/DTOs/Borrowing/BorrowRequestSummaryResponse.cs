namespace SmartLab.BLL.DTOs.Borrowing;

public class BorrowRequestSummaryResponse
{
    public Guid BorrowRequestId { get; set; }

    public string RequestCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid RoomId { get; set; }

    public string RoomCode { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public DateTime RequestDate { get; set; }

    public DateTime? BorrowDate { get; set; }

    public DateTime DueDate { get; set; }

    public string? RejectReason { get; set; }

    /// <summary>Approval steps in order: 1 = lecturer, 2 = department head (only with HIGH_VALUE components).</summary>
    public List<BorrowApprovalResponse> Approvals { get; set; } = new();
}

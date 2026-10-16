namespace SmartLab.BLL.DTOs.Borrowing;

public class BorrowRequestResponse
{
    public Guid BorrowRequestId { get; set; }

    public string RequestCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    /// <summary>Being borrowed (Borrowing / Overdue) and past the due date.</summary>
    public bool IsOverdue { get; set; }

    public Guid RequesterId { get; set; }

    public string RequesterName { get; set; } = string.Empty;

    public Guid? ClassId { get; set; }

    public string? ClassCode { get; set; }

    public Guid? ProjectId { get; set; }

    public string? ProjectTitle { get; set; }

    /// <summary>The pickup lab: where the student picks up and returns.</summary>
    public Guid RoomId { get; set; }

    public string RoomCode { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public DateTime RequestDate { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? BorrowDate { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public int ItemCount { get; set; }

    /// <summary>Lines still waiting to be carried from another lab to the pickup lab.</summary>
    public int WaitingTransferCount { get; set; }
}

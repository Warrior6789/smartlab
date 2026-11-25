namespace SmartLab.BLL.DTOs.Borrowing;

public class ReturnSlipQueryParams
{
    /// <summary>Slip code, request code, student name or student code, project title.</summary>
    public string? Search { get; set; }

    /// <summary>Pending, Approved, Rejected, Completed or Cancelled.</summary>
    public string? Status { get; set; }

    public Guid? BorrowRequestId { get; set; }

    public Guid? RoomId { get; set; }

    /// <summary>Appointment day from (Vietnam calendar day).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Appointment day to, that day included.</summary>
    public DateTime? ToDate { get; set; }
}

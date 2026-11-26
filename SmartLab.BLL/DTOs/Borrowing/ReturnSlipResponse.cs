namespace SmartLab.BLL.DTOs.Borrowing;

public class ReturnSlipResponse
{
    public Guid ReturnSlipId { get; set; }

    public string SlipCode { get; set; } = string.Empty;

    /// <summary>Pending, Approved, Rejected, Completed or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Approved but the appointment day has passed and the goods were not received.</summary>
    public bool IsLate { get; set; }

    /// <summary>Day the lab told the student to come; null until approved.</summary>
    public DateTime? AppointmentDate { get; set; }

    public Guid BorrowRequestId { get; set; }

    public string RequestCode { get; set; } = string.Empty;

    public DateTime DueDate { get; set; }

    public Guid RequesterId { get; set; }

    public string RequesterName { get; set; } = string.Empty;

    public string? StudentCode { get; set; }

    public string? ClassCode { get; set; }

    public string? ProjectTitle { get; set; }

    public Guid RoomId { get; set; }

    public string RoomCode { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    /// <summary>Lines on the slip.</summary>
    public int ItemCount { get; set; }

    /// <summary>Components of the request still out, on this slip or not.</summary>
    public int StillOutCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

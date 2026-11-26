namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>A return slip as shown in the detail of its borrow request.</summary>
public class ReturnSlipSummaryResponse
{
    public Guid ReturnSlipId { get; set; }

    public string SlipCode { get; set; } = string.Empty;

    /// <summary>Pending, Approved, Rejected, Completed or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Day the lab told the student to come; null until approved.</summary>
    public DateTime? AppointmentDate { get; set; }

    public int ItemCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

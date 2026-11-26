namespace SmartLab.BLL.DTOs.Borrowing;

public class RescheduleReturnSlipRequest
{
    /// <summary>The new day (Vietnam calendar day).</summary>
    public DateTime? AppointmentDate { get; set; }

    /// <summary>Sent to the student; replaces the note given when approving.</summary>
    public string? Comment { get; set; }
}

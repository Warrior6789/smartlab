namespace SmartLab.BLL.DTOs.Borrowing;

public class ApproveReturnSlipRequest
{
    /// <summary>Day the student must bring the goods to the lab (Vietnam calendar day), chosen by the lab.</summary>
    public DateTime? AppointmentDate { get; set; }

    public string? Comment { get; set; }
}

namespace SmartLab.BLL.DTOs.Borrowing;

public class LabAcceptRequest
{
    /// <summary>Day the student comes to take the components (Vietnam calendar day), chosen by the lab. Null = today.</summary>
    public DateTime? PickupDate { get; set; }
}

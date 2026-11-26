namespace SmartLab.BLL.DTOs.Borrowing;

public class ChangePickupDateRequest
{
    /// <summary>The new day (Vietnam calendar day).</summary>
    public DateTime? PickupDate { get; set; }
}

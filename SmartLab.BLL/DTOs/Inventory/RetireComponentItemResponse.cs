namespace SmartLab.BLL.DTOs.Inventory;

public class RetireComponentItemResponse
{
    public ComponentItemResponse Item { get; set; } = null!;

    /// <summary>Not empty when a unit marked Lost leaves the lab short for Approved requests.</summary>
    public List<ReservationShortageResponse> ReservationShortages { get; set; } = new();
}

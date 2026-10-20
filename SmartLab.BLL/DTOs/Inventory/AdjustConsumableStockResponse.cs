namespace SmartLab.BLL.DTOs.Inventory;

public class AdjustConsumableStockResponse
{
    public ConsumableStockResponse Stock { get; set; } = null!;

    /// <summary>Not empty when the counted quantity leaves the lab short for Approved requests.</summary>
    public List<ReservationShortageResponse> ReservationShortages { get; set; } = new();
}

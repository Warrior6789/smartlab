namespace SmartLab.BLL.DTOs.Inventory;

public class MoveComponentItemsResponse
{
    public List<ComponentItemResponse> Items { get; set; } = new();

    /// <summary>Not empty when units marked Broken leave the lab short for Approved requests.</summary>
    public List<ReservationShortageResponse> ReservationShortages { get; set; } = new();
}

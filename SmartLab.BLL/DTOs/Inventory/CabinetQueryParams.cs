namespace SmartLab.BLL.DTOs.Inventory;

public class CabinetQueryParams
{
    /// <summary>Matches cabinet code or name (case-insensitive).</summary>
    public string? Search { get; set; }
    public Guid? RoomId { get; set; }
    public string? Status { get; set; }
}

namespace SmartLab.BLL.DTOs.Inventory;

public class ConsumableStockQueryParams
{
    /// <summary>Matches component code or name (case-insensitive).</summary>
    public string? Search { get; set; }
    public Guid? ComponentId { get; set; }
    public Guid? CabinetId { get; set; }
    public Guid? RoomId { get; set; }

    /// <summary>When false (default), rows with quantity 0 are hidden.</summary>
    public bool IncludeEmpty { get; set; }
}

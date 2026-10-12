namespace SmartLab.BLL.DTOs.Inventory;

public class InventoryAuditQueryParams
{
    public Guid? CabinetId { get; set; }
    public Guid? RoomId { get; set; }

    /// <summary>InProgress or Completed.</summary>
    public string? Status { get; set; }
}

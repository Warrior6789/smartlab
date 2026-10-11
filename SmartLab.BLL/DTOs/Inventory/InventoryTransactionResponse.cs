namespace SmartLab.BLL.DTOs.Inventory;

public class InventoryTransactionResponse
{
    public Guid TransactionId { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public Guid? CabinetId { get; set; }
    public string? CabinetCode { get; set; }
    public Guid? ItemId { get; set; }
    public string? SerialNumber { get; set; }

    /// <summary>Positive when stock goes up, negative when it goes down.</summary>
    public int QuantityChange { get; set; }
    public string? Reason { get; set; }
    public Guid? ReceiptId { get; set; }
    public string? ReceiptCode { get; set; }
    public Guid CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

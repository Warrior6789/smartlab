namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// One HIGH_VALUE unit received on a receipt line, with its internal item code and the details entered on the receipt.
/// Its current status and cabinet are shown by GET /api/component-items.
/// </summary>
public class StockReceiptUnitResponse
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? HardwareVersion { get; set; }
    public DateTime? WarrantyExpiresAt { get; set; }
}

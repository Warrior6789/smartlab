namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>Sets the stock of one CONSUMABLE component in one cabinet to the quantity counted during a stock check.</summary>
public class AdjustConsumableStockRequest
{
    public Guid ComponentId { get; set; }
    public Guid CabinetId { get; set; }
    public int CountedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>Optional. The stock receipt that caused the difference, e.g. when a receipt was entered wrong.</summary>
    public Guid? ReceiptId { get; set; }
}

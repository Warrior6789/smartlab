namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>Takes one unit out of stock. Status is Lost or Retired; Reason is saved in inventory_transactions.</summary>
public class RetireComponentItemRequest
{
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    /// <summary>Optional. The stock receipt that caused this, e.g. a unit that never existed because the receipt was entered wrong.</summary>
    public Guid? ReceiptId { get; set; }
}

namespace SmartLab.BLL.DTOs.Inventory;

public class InventoryTransactionQueryParams
{
    public Guid? ComponentId { get; set; }
    public Guid? CabinetId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ReceiptId { get; set; }
    public Guid? AuditId { get; set; }
    public Guid? CreatedBy { get; set; }

    /// <summary>Receipt, ReceiptCancel, Adjust, Move, Retire, Borrow or Return.</summary>
    public string? Type { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd) of CreatedAt, inclusive.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd) of CreatedAt, inclusive.</summary>
    public DateTime? ToDate { get; set; }
}

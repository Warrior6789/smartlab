namespace SmartLab.BLL.DTOs.Inventory;

public class StockReceiptResponse
{
    public Guid ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }

    /// <summary>Completed or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the goods arrived (can be earlier than CreatedAt).</summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>When the receipt was entered into the system.</summary>
    public DateTime CreatedAt { get; set; }

    public decimal TotalAmount { get; set; }
    public Guid CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int LineCount { get; set; }
}

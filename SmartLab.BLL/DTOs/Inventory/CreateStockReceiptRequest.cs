namespace SmartLab.BLL.DTOs.Inventory;

public class CreateStockReceiptRequest
{
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>Number printed on the supplier invoice; the same supplier + number cannot be entered twice.</summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>Date printed on the supplier invoice; cannot be in the future.</summary>
    public DateTime? InvoiceDate { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// When the goods actually arrived; defaults to now and cannot be in the future.
    /// A value without offset (e.g. "2026-10-03T09:00") is read as Vietnam time.
    /// </summary>
    public DateTime? ReceivedAt { get; set; }

    public List<StockReceiptLineRequest> Items { get; set; } = new();
}

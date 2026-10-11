namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// Corrects the receipt info only. The lines cannot be edited: a receipt with wrong goods is cancelled and entered again.
/// A null ReceivedAt keeps the current value.
/// </summary>
public class UpdateStockReceiptRequest
{
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>Number printed on the supplier invoice; the same supplier + number cannot be entered twice.</summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>Date printed on the supplier invoice; cannot be in the future.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Name of the person who delivered the goods (người giao hàng).</summary>
    public string? DelivererName { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// When the goods actually arrived; cannot be in the future.
    /// A value without offset (e.g. "2026-10-03T09:00") is read as Vietnam time.
    /// </summary>
    public DateTime? ReceivedAt { get; set; }
}

namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>Receipt laid out like form 01-VT (Phiếu nhập kho), plus the system's own tracking fields.</summary>
public class StockReceiptDetailResponse : StockReceiptResponse
{
    public DateTime? InvoiceDate { get; set; }
    public string? DelivererName { get; set; }

    /// <summary>TotalAmount written in Vietnamese words, e.g. "Năm triệu đồng".</summary>
    public string TotalAmountInWords { get; set; } = string.Empty;

    public string? Note { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancelledByName { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>Uploaded supplier invoice (image or PDF); null when none was attached.</summary>
    public string? InvoiceUrl { get; set; }

    /// <summary>Last change of the receipt info or invoice file; null when never changed after creation.</summary>
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public string? UpdatedByName { get; set; }
    public IReadOnlyList<StockReceiptLineResponse> Items { get; set; } = Array.Empty<StockReceiptLineResponse>();
}

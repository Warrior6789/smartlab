using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface IStockReceiptService
{
    /// <summary>Paged stock receipts, newest first, filtered by search text and received date.</summary>
    Task<PagedResult<StockReceiptResponse>> GetReceiptsAsync(StockReceiptQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>Stock receipt detail with its lines.</summary>
    Task<StockReceiptDetailResponse> GetReceiptByIdAsync(Guid receiptId, CancellationToken ct = default);

    /// <summary>
    /// Records goods that arrived and puts them in stock right away:
    /// CONSUMABLE adds to consumable_stocks, REUSABLE/HIGH_VALUE creates one Available component item per unit.
    /// </summary>
    Task<StockReceiptDetailResponse> CreateReceiptAsync(CreateStockReceiptRequest request, CancellationToken ct = default);

    /// <summary>
    /// Corrects the receipt info (supplier, note, received date) without touching the stock.
    /// Wrong goods are fixed by cancelling the receipt and creating a new one, or by a stock adjustment when the goods were already used.
    /// </summary>
    Task<StockReceiptDetailResponse> UpdateReceiptAsync(Guid receiptId, UpdateStockReceiptRequest request, CancellationToken ct = default);

    /// <summary>Attaches the supplier invoice (image or PDF), replacing the previous file.</summary>
    Task<StockReceiptDetailResponse> UploadInvoiceAsync(Guid receiptId, UploadInvoiceRequest request, CancellationToken ct = default);

    /// <summary>Removes the attached invoice and deletes the stored file.</summary>
    Task DeleteInvoiceAsync(Guid receiptId, CancellationToken ct = default);

    /// <summary>
    /// Cancels a wrong receipt and takes its goods back out of stock. Allowed only while all of them are untouched:
    /// CONSUMABLE quantity still in the same cabinet, every unit still Available and never borrowed or reported.
    /// Units are deleted; the receipt and its lines stay as history with status Cancelled.
    /// </summary>
    Task<StockReceiptDetailResponse> CancelReceiptAsync(Guid receiptId, CancelStockReceiptRequest request, CancellationToken ct = default);
}

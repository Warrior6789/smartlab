using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/stock-receipts")]
[Produces("application/json")]
[Authorize(Policy = PermissionCodes.StockReceiptManage)]
public class StockReceiptsController : ControllerBase
{
    private readonly IStockReceiptService _stockReceiptService;

    public StockReceiptsController(IStockReceiptService stockReceiptService)
    {
        _stockReceiptService = stockReceiptService;
    }

    /// <summary>Danh sách phiếu nhập kho (phân trang, tìm theo mã phiếu/nhà cung cấp, lọc theo ngày nhập).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockReceiptResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReceipts(
        [FromQuery] StockReceiptQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _stockReceiptService.GetReceiptsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<StockReceiptResponse>>.Ok(result));
    }

    /// <summary>Chi tiết phiếu nhập kho.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StockReceiptDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceipt(Guid id, CancellationToken ct)
    {
        var receipt = await _stockReceiptService.GetReceiptByIdAsync(id, ct);
        return Ok(ApiResponse<StockReceiptDetailResponse>.Ok(receipt));
    }

    /// <summary>Tạo phiếu nhập kho: hàng được cộng vào kho ngay. Nhập sai hàng thì hủy phiếu rồi tạo phiếu mới.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<StockReceiptDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateReceipt([FromBody] CreateStockReceiptRequest request, CancellationToken ct)
    {
        var receipt = await _stockReceiptService.CreateReceiptAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<StockReceiptDetailResponse>.Ok(receipt, "Nhập kho thành công"));
    }

    /// <summary>
    /// Sửa thông tin phiếu (nhà cung cấp, ghi chú, ngày nhận), không đụng tới kho.
    /// Không sửa được dòng hàng: sai hàng thì hủy phiếu rồi tạo phiếu mới, hàng đã dùng thì điều chỉnh kho.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StockReceiptDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateReceipt(Guid id, [FromBody] UpdateStockReceiptRequest request, CancellationToken ct)
    {
        var receipt = await _stockReceiptService.UpdateReceiptAsync(id, request, ct);
        return Ok(ApiResponse<StockReceiptDetailResponse>.Ok(receipt, "Sửa phiếu nhập kho thành công"));
    }

    /// <summary>Đính kèm hóa đơn của nhà cung cấp (ảnh JPG/PNG/WEBP hoặc PDF, tối đa 10 MB). Thay file cũ nếu có.</summary>
    [HttpPut("{id:guid}/invoice")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<StockReceiptDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadInvoice(Guid id, [FromForm] UploadInvoiceRequest request, CancellationToken ct)
    {
        var receipt = await _stockReceiptService.UploadInvoiceAsync(id, request, ct);
        return Ok(ApiResponse<StockReceiptDetailResponse>.Ok(receipt, "Tải hóa đơn thành công"));
    }

    /// <summary>Xóa hóa đơn đã đính kèm.</summary>
    [HttpDelete("{id:guid}/invoice")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteInvoice(Guid id, CancellationToken ct)
    {
        await _stockReceiptService.DeleteInvoiceAsync(id, ct);
        return Ok(ApiResponse.Ok("Xóa hóa đơn thành công"));
    }

    /// <summary>
    /// Hủy phiếu nhập sai: trừ lại tồn kho và xóa các con linh kiện của phiếu (chỉ khi hàng còn nguyên).
    /// Không hủy được nếu đã có linh kiện được sửa ở kho (điều chỉnh/chuyển tủ/thanh lý) theo phiếu này.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionCodes.InventoryAudit)]
    [ProducesResponseType(typeof(ApiResponse<StockReceiptDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelReceipt(Guid id, [FromBody] CancelStockReceiptRequest request, CancellationToken ct)
    {
        var receipt = await _stockReceiptService.CancelReceiptAsync(id, request, ct);
        return Ok(ApiResponse<StockReceiptDetailResponse>.Ok(receipt, "Hủy phiếu nhập kho thành công"));
    }
}

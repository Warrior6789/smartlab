using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Reports;
using SmartLab.BLL.Interfaces.Reports;

namespace SmartLab.API.Controllers.Reports;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
[Authorize(Policy = PermissionCodes.ReportView)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>Tồn kho theo linh kiện: sẵn sàng, đang mượn, mất/thanh lý (Admin, quản lý kho; LabStaff chỉ lab mình phụ trách).</summary>
    [HttpGet("inventory/stock-summary")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStockSummary(
        [FromQuery] StockSummaryQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _reportService.GetStockSummaryAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<StockSummaryResponse>>.Ok(result));
    }

    /// <summary>Linh kiện dưới mức tồn tối thiểu, số còn thiếu và giá nhập gần nhất (LabStaff không thấy giá), thiếu nhiều nhất trước.</summary>
    [HttpGet("inventory/low-stock")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<LowStockResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLowStock(
        [FromQuery] StockSummaryQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _reportService.GetLowStockAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<LowStockResponse>>.Ok(result));
    }

    /// <summary>Thẻ kho của 1 linh kiện trong kỳ: tồn đầu, nhập/xuất theo loại giao dịch, tồn cuối (mặc định 30 ngày gần nhất, tối đa 366 ngày).</summary>
    [HttpGet("inventory/movements")]
    [ProducesResponseType(typeof(ApiResponse<StockMovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockMovements([FromQuery] StockMovementQueryParams query, CancellationToken ct)
    {
        var result = await _reportService.GetStockMovementsAsync(query, ct);
        return Ok(ApiResponse<StockMovementResponse>.Ok(result));
    }

    /// <summary>Giá trị kho hiện tại: con theo giá mua, vật tư tiêu hao theo giá nhập gần nhất; gom theo danh mục và theo lab (chỉ Admin, quản lý kho).</summary>
    [HttpGet("inventory/stock-value")]
    [ProducesResponseType(typeof(ApiResponse<StockValueResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStockValue(CancellationToken ct)
    {
        var result = await _reportService.GetStockValueAsync(ct);
        return Ok(ApiResponse<StockValueResponse>.Ok(result));
    }

    /// <summary>Nhập kho theo kỳ (phiếu Completed): số phiếu, tổng tiền, gom theo groupBy = month / supplier / category (chỉ Admin, quản lý kho).</summary>
    [HttpGet("inventory/receipts")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReceiptReport([FromQuery] ReceiptReportQueryParams query, CancellationToken ct)
    {
        var result = await _reportService.GetReceiptReportAsync(query, ct);
        return Ok(ApiResponse<ReceiptReportResponse>.Ok(result));
    }

    /// <summary>Hao hụt trong kỳ: con chuyển sang Lost/Retired, vật tư bị điều chỉnh giảm; số lượng và giá trị (chỉ Admin, quản lý kho).</summary>
    [HttpGet("inventory/losses")]
    [ProducesResponseType(typeof(ApiResponse<LossReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLosses([FromQuery] ReportPeriodQueryParams query, CancellationToken ct)
    {
        var result = await _reportService.GetLossesAsync(query, ct);
        return Ok(ApiResponse<LossReportResponse>.Ok(result));
    }

    /// <summary>Thiết bị giá trị cao sắp hết bảo hành trong days ngày tới (mặc định 30), hết sớm nhất trước.</summary>
    [HttpGet("inventory/warranty-expiring")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WarrantyExpiringResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetWarrantyExpiring(
        [FromQuery] WarrantyQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _reportService.GetWarrantyExpiringAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<WarrantyExpiringResponse>>.Ok(result));
    }

    /// <summary>Thống kê đơn mượn tạo trong kỳ: theo trạng thái, lab, lớp; tỉ lệ từ chối và thời gian duyệt trung bình.</summary>
    [HttpGet("borrowing/summary")]
    [ProducesResponseType(typeof(ApiResponse<BorrowSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBorrowSummary([FromQuery] BorrowReportQueryParams query, CancellationToken ct)
    {
        var result = await _reportService.GetBorrowSummaryAsync(query, ct);
        return Ok(ApiResponse<BorrowSummaryResponse>.Ok(result));
    }

    /// <summary>Mức mượn theo lớp (theo ngày giao trong kỳ): số đơn, đã giao, đã trả, đang giữ, hỏng, mất.</summary>
    [HttpGet("borrowing/by-class")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClassUsageResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClassUsage([FromQuery] BorrowReportQueryParams query, CancellationToken ct)
    {
        var result = await _reportService.GetClassUsageAsync(query, ct);
        return Ok(ApiResponse<IReadOnlyList<ClassUsageResponse>>.Ok(result));
    }

    /// <summary>Linh kiện trả về bị hỏng hoặc mất trong kỳ: người mượn, lớp, linh kiện (chỉ thống kê, không bồi thường).</summary>
    [HttpGet("borrowing/damages")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DamageResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDamages(
        [FromQuery] BorrowReportQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _reportService.GetDamagesAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<DamageResponse>>.Ok(result));
    }

    /// <summary>Thống kê sự cố tạo trong kỳ: theo trạng thái, loại, linh kiện nhiều sự cố nhất, thời gian xử lý trung bình.</summary>
    [HttpGet("issues")]
    [ProducesResponseType(typeof(ApiResponse<IssueSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIssueSummary([FromQuery] ReportPeriodQueryParams query, CancellationToken ct)
    {
        var result = await _reportService.GetIssueSummaryAsync(query, ct);
        return Ok(ApiResponse<IssueSummaryResponse>.Ok(result));
    }

    /// <summary>Đơn mượn quá hạn:người mượn, lớp, project, số ngày trễ, linh kiện chưa trả (trễ lâu nhất trước; GV chỉ lớp mình, LabStaff chỉ lab mình).</summary>
    [HttpGet("borrowing/overdue")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OverdueBorrowResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOverdueBorrows(
        [FromQuery] OverdueBorrowQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _reportService.GetOverdueBorrowsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<OverdueBorrowResponse>>.Ok(result));
    }

    /// <summary>Linh kiện được mượn nhiều nhất trong kỳ (theo ngày giao): số lượt đơn và tổng số lượng.</summary>
    [HttpGet("borrowing/top-components")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TopComponentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTopComponents([FromQuery] TopComponentQueryParams query, CancellationToken ct)
    {
        var result = await _reportService.GetTopComponentsAsync(query, ct);
        return Ok(ApiResponse<IReadOnlyList<TopComponentResponse>>.Ok(result));
    }

    /// <summary>Các ô số liệu trang chủ theo role; ô nào role không được xem thì null.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<DashboardResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await _reportService.GetDashboardAsync(ct);
        return Ok(ApiResponse<DashboardResponse>.Ok(result));
    }
}

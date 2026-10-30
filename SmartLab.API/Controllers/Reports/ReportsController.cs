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

    /// <summary>Đơn mượn quá hạn: người mượn, lớp, project, số ngày trễ, linh kiện chưa trả (trễ lâu nhất trước; GV chỉ lớp mình, LabStaff chỉ lab mình).</summary>
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

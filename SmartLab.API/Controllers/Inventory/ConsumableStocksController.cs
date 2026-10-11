using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/consumable-stocks")]
[Produces("application/json")]
public class ConsumableStocksController : ControllerBase
{
    private readonly IConsumableStockService _consumableStockService;

    public ConsumableStocksController(IConsumableStockService consumableStockService)
    {
        _consumableStockService = consumableStockService;
    }

    /// <summary>Danh sách tồn kho hàng tiêu hao theo tủ (phân trang, lọc theo linh kiện/tủ/phòng, mặc định ẩn dòng số lượng 0).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ComponentView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ConsumableStockResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStocks(
        [FromQuery] ConsumableStockQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _consumableStockService.GetStocksAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<ConsumableStockResponse>>.Ok(result));
    }

    /// <summary>Chuyển hàng tiêu hao từ tủ này sang tủ khác. Có thể kèm lý do và phiếu nhập đang sửa (vd phiếu chọn nhầm tủ).</summary>
    [HttpPost("move")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConsumableStockResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MoveStock([FromBody] MoveConsumableStockRequest request, CancellationToken ct)
    {
        var stocks = await _consumableStockService.MoveStockAsync(request, ct);
        return Ok(ApiResponse<IReadOnlyList<ConsumableStockResponse>>.Ok(stocks, "Chuyển tủ thành công"));
    }

    /// <summary>Điều chỉnh kiểm kê: đặt tồn kho bằng số đếm thực tế, có ghi lý do vào nhật ký.</summary>
    [HttpPost("adjust")]
    [Authorize(Policy = PermissionCodes.InventoryAudit)]
    [ProducesResponseType(typeof(ApiResponse<ConsumableStockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdjustStock([FromBody] AdjustConsumableStockRequest request, CancellationToken ct)
    {
        var stock = await _consumableStockService.AdjustStockAsync(request, ct);
        return Ok(ApiResponse<ConsumableStockResponse>.Ok(stock, "Điều chỉnh tồn kho thành công"));
    }
}

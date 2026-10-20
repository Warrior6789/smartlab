using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/inventory-transactions")]
[Produces("application/json")]
public class InventoryTransactionsController : ControllerBase
{
    private readonly IInventoryTransactionService _inventoryTransactionService;

    public InventoryTransactionsController(IInventoryTransactionService inventoryTransactionService)
    {
        _inventoryTransactionService = inventoryTransactionService;
    }

    /// <summary>Sổ kho: lịch sử nhập, hủy phiếu, điều chỉnh, chuyển tủ, thanh lý (mới nhất trước, phân trang, lọc theo linh kiện/tủ/con/phiếu/người làm/loại/ngày).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.InventoryAudit)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InventoryTransactionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] InventoryTransactionQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _inventoryTransactionService.GetTransactionsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<InventoryTransactionResponse>>.Ok(result));
    }
}

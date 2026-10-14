using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/inventory-audits")]
[Produces("application/json")]
[Authorize(Policy = PermissionCodes.InventoryAudit)]
public class InventoryAuditsController : ControllerBase
{
    private readonly IInventoryAuditService _inventoryAuditService;

    public InventoryAuditsController(IInventoryAuditService inventoryAuditService)
    {
        _inventoryAuditService = inventoryAuditService;
    }

    /// <summary>Danh sách phiên kiểm kê (mới nhất trước, phân trang, lọc theo tủ/phòng/trạng thái).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InventoryAuditResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAudits(
        [FromQuery] InventoryAuditQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _inventoryAuditService.GetAuditsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<InventoryAuditResponse>>.Ok(result));
    }

    /// <summary>
    /// Chi tiết phiên kiểm kê. Đang kiểm: danh sách hàng cần đếm (vật tư + từng con trong tủ).
    /// Đã hoàn tất: biên bản gồm các dòng lệch đã ghi vào sổ kho.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<InventoryAuditDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAudit(Guid id, CancellationToken ct)
    {
        var audit = await _inventoryAuditService.GetAuditByIdAsync(id, ct);
        return Ok(ApiResponse<InventoryAuditDetailResponse>.Ok(audit));
    }

    /// <summary>Bắt đầu kiểm kê một tủ. Mỗi tủ chỉ có một phiên đang kiểm.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InventoryAuditDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartAudit([FromBody] StartInventoryAuditRequest request, CancellationToken ct)
    {
        var audit = await _inventoryAuditService.StartAuditAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<InventoryAuditDetailResponse>.Ok(audit, "Bắt đầu kiểm kê"));
    }

    /// <summary>
    /// Hoàn tất kiểm kê: gửi số đếm vật tư và danh sách con không tìm thấy. Vật tư lệch được điều chỉnh,
    /// con không tìm thấy chuyển sang Lost, mọi thay đổi ghi vào sổ kho kèm mã phiên.
    /// Nếu làm lab thiếu hàng cho đơn mượn đã duyệt thì <c>reservationShortages</c> liệt kê linh kiện thiếu và các mã đơn bị ảnh hưởng.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(ApiResponse<InventoryAuditDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteAudit(Guid id, [FromBody] CompleteInventoryAuditRequest request, CancellationToken ct)
    {
        var audit = await _inventoryAuditService.CompleteAuditAsync(id, request, ct);
        return Ok(ApiResponse<InventoryAuditDetailResponse>.Ok(audit, "Hoàn tất kiểm kê"));
    }

    /// <summary>Hủy phiên đang kiểm (chưa ghi gì vào kho).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAudit(Guid id, CancellationToken ct)
    {
        await _inventoryAuditService.CancelAuditAsync(id, ct);
        return Ok(ApiResponse.Ok("Đã hủy phiên kiểm kê"));
    }
}

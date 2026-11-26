using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.API.Filters;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[BroadcastChange(RealtimeEntities.Inventory)]
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

    /// <summary>
    /// Danh sách phiên kiểm kê (mới nhất trước, phân trang, lọc theo tủ/phòng/trạng thái InProgress | Submitted | Completed).
    /// Nhân viên lab chỉ thấy phiên của các lab mình phụ trách; Quản lý kho / Admin thấy tất cả.
    /// </summary>
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
    /// Chi tiết phiên kiểm kê. Đang kiểm (InProgress): danh sách hàng cần đếm (vật tư + từng con trong tủ).
    /// Chờ duyệt (Submitted): số đếm đã gửi (submittedCounts) và các con báo thiếu (missingItems).
    /// Đã hoàn tất (Completed): biên bản gồm các dòng lệch đã ghi vào sổ kho.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<InventoryAuditDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAudit(Guid id, CancellationToken ct)
    {
        var audit = await _inventoryAuditService.GetAuditByIdAsync(id, ct);
        return Ok(ApiResponse<InventoryAuditDetailResponse>.Ok(audit));
    }

    /// <summary>
    /// Bắt đầu kiểm kê một tủ. Nhân viên lab chỉ kiểm được tủ thuộc lab mình phụ trách. Mỗi tủ chỉ có một phiên chưa xong
    /// (đang kiểm hoặc chờ duyệt).
    /// </summary>
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
    /// Gửi kết quả đếm để chờ duyệt (InProgress → Submitted): số đếm vật tư và danh sách con không tìm thấy.
    /// Chưa thay đổi tồn kho. <c>/complete</c> là đường dẫn cũ, làm đúng việc này (không còn chốt kho ngay).
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(ApiResponse<InventoryAuditDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitAudit(Guid id, [FromBody] SubmitInventoryAuditRequest request, CancellationToken ct)
    {
        var audit = await _inventoryAuditService.SubmitAuditAsync(id, request, ct);
        return Ok(ApiResponse<InventoryAuditDetailResponse>.Ok(audit, "Đã gửi kết quả kiểm kê, chờ duyệt"));
    }

    /// <summary>
    /// Quản lý kho / Admin duyệt kết quả đếm (Submitted → Completed): vật tư lệch được điều chỉnh theo phần chênh lệch lúc đếm,
    /// con không tìm thấy chuyển sang Lost, mọi thay đổi ghi vào sổ kho kèm mã phiên. Người duyệt phải khác người đã đếm.
    /// Nếu sau khi đếm có con báo thiếu đã được giao / chuyển tủ thì báo 409, hãy yêu cầu đếm lại.
    /// Nếu làm lab thiếu hàng cho đơn mượn đã duyệt thì <c>reservationShortages</c> liệt kê linh kiện thiếu và các mã đơn bị ảnh hưởng.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = PermissionCodes.InventoryAuditApprove)]
    [ProducesResponseType(typeof(ApiResponse<InventoryAuditDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveAudit(Guid id, [FromBody] ApproveInventoryAuditRequest request, CancellationToken ct)
    {
        var audit = await _inventoryAuditService.ApproveAuditAsync(id, request, ct);
        return Ok(ApiResponse<InventoryAuditDetailResponse>.Ok(audit, "Đã duyệt kết quả kiểm kê"));
    }

    /// <summary>Quản lý kho / Admin trả kết quả về để đếm lại (Submitted → InProgress), bắt buộc ghi lý do.</summary>
    [HttpPost("{id:guid}/recount")]
    [Authorize(Policy = PermissionCodes.InventoryAuditApprove)]
    [ProducesResponseType(typeof(ApiResponse<InventoryAuditDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestRecount(Guid id, [FromBody] RecountInventoryAuditRequest request, CancellationToken ct)
    {
        var audit = await _inventoryAuditService.RequestRecountAsync(id, request, ct);
        return Ok(ApiResponse<InventoryAuditDetailResponse>.Ok(audit, "Đã yêu cầu đếm lại"));
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

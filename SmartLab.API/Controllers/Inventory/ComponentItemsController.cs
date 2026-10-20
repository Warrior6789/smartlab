using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/component-items")]
[Produces("application/json")]
public class ComponentItemsController : ControllerBase
{
    private readonly IComponentItemService _componentItemService;

    public ComponentItemsController(IComponentItemService componentItemService)
    {
        _componentItemService = componentItemService;
    }

    /// <summary>Danh sách từng con linh kiện (phân trang, lọc theo linh kiện/tủ/trạng thái, tìm theo serial).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ComponentView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ComponentItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetItems(
        [FromQuery] ComponentItemQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _componentItemService.GetItemsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<ComponentItemResponse>>.Ok(result));
    }

    /// <summary>Chi tiết 1 con linh kiện.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ComponentView)]
    [ProducesResponseType(typeof(ApiResponse<ComponentItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken ct)
    {
        var item = await _componentItemService.GetItemByIdAsync(id, ct);
        return Ok(ApiResponse<ComponentItemResponse>.Ok(item));
    }

    /// <summary>Sửa thông tin 1 con linh kiện (serial, version, bảo hành, tình trạng, ghi chú). Đổi tủ/trạng thái dùng API move.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<ComponentItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] UpdateComponentItemRequest request, CancellationToken ct)
    {
        var item = await _componentItemService.UpdateItemAsync(id, request, ct);
        return Ok(ApiResponse<ComponentItemResponse>.Ok(item, "Cập nhật linh kiện thành công"));
    }

    /// <summary>
    /// Chuyển tủ và/hoặc đổi trạng thái (Available/Broken/Maintenance). Gửi itemIds để chọn đúng con (vd 1 serial), hoặc quantity để hệ thống tự chọn con cũ nhất. Có thể kèm lý do và phiếu nhập đang sửa. Báo mất hoặc thanh lý dùng API retire.
    /// Hàng đang được giữ chỗ cho đơn đã duyệt: không chuyển sang Maintenance hay sang lab khác được (409); báo Broken vẫn được,
    /// khi đó <c>reservationShortages</c> liệt kê linh kiện thiếu và các mã đơn bị ảnh hưởng.
    /// </summary>
    [HttpPost("move")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<MoveComponentItemsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MoveItems([FromBody] MoveComponentItemsRequest request, CancellationToken ct)
    {
        var result = await _componentItemService.MoveItemsAsync(request, ct);
        return Ok(ApiResponse<MoveComponentItemsResponse>.Ok(result, $"Đã cập nhật {result.Items.Count} con linh kiện"));
    }

    /// <summary>
    /// Loại đúng 1 con khỏi kho (Lost hoặc Retired), bắt buộc ghi lý do (lưu vào sổ kho). Con đang được mượn phải trả về trước.
    /// Con đang được giữ chỗ cho đơn đã duyệt: không thanh lý (Retired) được (409); báo Lost vẫn được, kèm <c>reservationShortages</c>.
    /// </summary>
    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<RetireComponentItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetireItem(Guid id, [FromBody] RetireComponentItemRequest request, CancellationToken ct)
    {
        var result = await _componentItemService.RetireItemAsync(id, request, ct);
        return Ok(ApiResponse<RetireComponentItemResponse>.Ok(result, "Đã loại linh kiện khỏi kho"));
    }
}

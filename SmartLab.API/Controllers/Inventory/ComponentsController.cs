using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/components")]
[Produces("application/json")]
public class ComponentsController : ControllerBase
{
    private readonly IComponentService _componentService;

    public ComponentsController(IComponentService componentService)
    {
        _componentService = componentService;
    }

    /// <summary>Danh mục linh kiện (phân trang, tìm kiếm, lọc theo danh mục/trạng thái/loại) kèm số lượng khả dụng.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ComponentView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ComponentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetComponents(
        [FromQuery] ComponentQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _componentService.GetComponentsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<ComponentResponse>>.Ok(result));
    }

    /// <summary>Chi tiết linh kiện: thông số và tồn kho theo tủ.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ComponentView)]
    [ProducesResponseType(typeof(ApiResponse<ComponentDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComponent(Guid id, CancellationToken ct)
    {
        var component = await _componentService.GetComponentByIdAsync(id, ct);
        return Ok(ApiResponse<ComponentDetailResponse>.Ok(component));
    }

    /// <summary>Khai báo linh kiện mới kèm thông số. Ảnh upload sau qua POST /api/components/{componentId}/images.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<ComponentDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateComponent([FromBody] CreateComponentRequest request, CancellationToken ct)
    {
        var component = await _componentService.CreateComponentAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ComponentDetailResponse>.Ok(component, "Tạo linh kiện thành công"));
    }

    /// <summary>Cập nhật thông tin linh kiện (thay toàn bộ thông số).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<ComponentDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateComponent(Guid id, [FromBody] UpdateComponentRequest request, CancellationToken ct)
    {
        var component = await _componentService.UpdateComponentAsync(id, request, ct);
        return Ok(ApiResponse<ComponentDetailResponse>.Ok(component, "Cập nhật linh kiện thành công"));
    }

    /// <summary>Tải file datasheet PDF (tối đa 10 MB). Thay file cũ nếu có, hoặc thay link đang dán.</summary>
    [HttpPut("{id:guid}/datasheet")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ComponentDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadDatasheet(Guid id, [FromForm] UploadDatasheetRequest request, CancellationToken ct)
    {
        var component = await _componentService.UploadDatasheetAsync(id, request, ct);
        return Ok(ApiResponse<ComponentDetailResponse>.Ok(component, "Tải datasheet thành công"));
    }

    /// <summary>Xóa datasheet (link hoặc file). File đã tải lên bị xóa khỏi kho lưu trữ.</summary>
    [HttpDelete("{id:guid}/datasheet")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDatasheet(Guid id, CancellationToken ct)
    {
        await _componentService.DeleteDatasheetAsync(id, ct);
        return Ok(ApiResponse.Ok("Xóa datasheet thành công"));
    }
}

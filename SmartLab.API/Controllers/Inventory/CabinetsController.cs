using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/cabinets")]
[Produces("application/json")]
public class CabinetsController : ControllerBase
{
    private readonly ICabinetService _cabinetService;

    public CabinetsController(ICabinetService cabinetService)
    {
        _cabinetService = cabinetService;
    }

    /// <summary>Danh sách tủ/kệ lưu trữ (phân trang, tìm theo mã/tên, lọc theo phòng/trạng thái).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ComponentView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CabinetResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCabinets(
        [FromQuery] CabinetQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var cabinets = await _cabinetService.GetCabinetsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<CabinetResponse>>.Ok(cabinets));
    }

    /// <summary>Chi tiết tủ kèm các linh kiện đang cất trong tủ.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ComponentView)]
    [ProducesResponseType(typeof(ApiResponse<CabinetDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCabinet(Guid id, CancellationToken ct)
    {
        var cabinet = await _cabinetService.GetCabinetByIdAsync(id, ct);
        return Ok(ApiResponse<CabinetDetailResponse>.Ok(cabinet));
    }

    /// <summary>Tạo tủ/kệ lưu trữ.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<CabinetResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCabinet([FromBody] CreateCabinetRequest request, CancellationToken ct)
    {
        var cabinet = await _cabinetService.CreateCabinetAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CabinetResponse>.Ok(cabinet, "Tạo tủ thành công"));
    }

    /// <summary>Sửa tủ/kệ, bao gồm ngừng sử dụng (Inactive) khi tủ đã trống.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<CabinetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCabinet(Guid id, [FromBody] UpdateCabinetRequest request, CancellationToken ct)
    {
        var cabinet = await _cabinetService.UpdateCabinetAsync(id, request, ct);
        return Ok(ApiResponse<CabinetResponse>.Ok(cabinet, "Cập nhật tủ thành công"));
    }
}

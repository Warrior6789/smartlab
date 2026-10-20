using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/lab-rooms")]
[Produces("application/json")]
public class LabRoomsController : ControllerBase
{
    private readonly ILabRoomService _labRoomService;

    public LabRoomsController(ILabRoomService labRoomService)
    {
        _labRoomService = labRoomService;
    }

    /// <summary>Danh sách phòng lab (dùng cho dropdown chọn phòng khi tạo tủ).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ComponentView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabRoomResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLabRooms(CancellationToken ct)
    {
        var rooms = await _labRoomService.GetLabRoomsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<LabRoomResponse>>.Ok(rooms));
    }

    /// <summary>Tạo phòng lab.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<LabRoomResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLabRoom([FromBody] CreateLabRoomRequest request, CancellationToken ct)
    {
        var room = await _labRoomService.CreateLabRoomAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<LabRoomResponse>.Ok(room, "Tạo phòng lab thành công"));
    }

    /// <summary>Cập nhật phòng lab (không ngừng sử dụng được khi phòng còn tủ đang dùng).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<LabRoomResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateLabRoom(Guid id, [FromBody] UpdateLabRoomRequest request, CancellationToken ct)
    {
        var room = await _labRoomService.UpdateLabRoomAsync(id, request, ct);
        return Ok(ApiResponse<LabRoomResponse>.Ok(room, "Cập nhật phòng lab thành công"));
    }
}

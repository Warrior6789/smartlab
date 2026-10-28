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

    /// <summary>Danh sách nhân viên lab được phân công ở phòng.</summary>
    [HttpGet("{id:guid}/staff")]
    [Authorize(Policy = PermissionCodes.UserManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabRoomStaffResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStaff(Guid id, CancellationToken ct)
    {
        var staff = await _labRoomService.GetStaffAsync(id, ct);
        return Ok(ApiResponse<IReadOnlyList<LabRoomStaffResponse>>.Ok(staff));
    }

    /// <summary>Phân công một tài khoản LabStaff vào phòng.</summary>
    [HttpPost("{id:guid}/staff")]
    [Authorize(Policy = PermissionCodes.UserManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabRoomStaffResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignStaff(Guid id, [FromBody] AssignLabRoomStaffRequest request, CancellationToken ct)
    {
        var staff = await _labRoomService.AssignStaffAsync(id, request, ct);
        return Ok(ApiResponse<IReadOnlyList<LabRoomStaffResponse>>.Ok(staff, "Phân công nhân viên thành công"));
    }

    /// <summary>Gỡ phân công nhân viên khỏi phòng.</summary>
    [HttpDelete("{id:guid}/staff/{userId:guid}")]
    [Authorize(Policy = PermissionCodes.UserManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LabRoomStaffResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveStaff(Guid id, Guid userId, CancellationToken ct)
    {
        var staff = await _labRoomService.RemoveStaffAsync(id, userId, ct);
        return Ok(ApiResponse<IReadOnlyList<LabRoomStaffResponse>>.Ok(staff, "Gỡ phân công nhân viên thành công"));
    }
}

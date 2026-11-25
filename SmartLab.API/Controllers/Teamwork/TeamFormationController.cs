using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.API.Filters;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Teamwork;
using SmartLab.BLL.Interfaces.Teamwork;

namespace SmartLab.API.Controllers.Teamwork;

/// <summary>
/// Chia nhóm của lớp: giảng viên đặt số thành viên và hạn chót; sinh viên tự lập nhóm tới hạn chót; sau đó giảng viên
/// chia các bạn còn lại (có đề xuất tự động) và chỉ giảng viên được đổi nhóm.
/// Chỉ giảng viên phụ trách lớp hoặc người quản lý học vụ (Admin) được thay đổi.
/// </summary>
[ApiController]
[BroadcastChange(RealtimeEntities.Academic)]
[Route("api/classes/{id:guid}/team-formation")]
[Produces("application/json")]
[Authorize]
public class TeamFormationController : ControllerBase
{
    private const string Managers = RoleNames.Lecturer + "," + RoleNames.Admin;

    private readonly ITeamFormationService _service;

    public TeamFormationController(ITeamFormationService service)
    {
        _service = service;
    }

    /// <summary>Cấu hình và tình trạng chia nhóm. Giảng viên/Admin thấy thêm danh sách nhóm và sinh viên chưa có nhóm.</summary>
    [HttpGet]
    [Authorize(Roles = RoleNames.Student + "," + Managers)]
    [ProducesResponseType(typeof(ApiResponse<TeamFormationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(ApiResponse<TeamFormationResponse>.Ok(await _service.GetAsync(id, ct)));

    /// <summary>Đặt số thành viên tối thiểu/tối đa mỗi nhóm và hạn chót chọn nhóm; sinh viên của lớp được báo. 409 nếu sĩ số không chia được theo cỡ nhóm.</summary>
    [HttpPut("settings")]
    [Authorize(Roles = Managers)]
    [ProducesResponseType(typeof(ApiResponse<TeamFormationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSettings(
        Guid id, [FromBody] UpdateTeamFormationSettingsRequest request, CancellationToken ct)
        => Ok(ApiResponse<TeamFormationResponse>.Ok(
            await _service.UpdateSettingsAsync(id, request, ct), "Cập nhật cấu hình chia nhóm thành công"));

    /// <summary>Kết thúc sớm thời gian sinh viên chọn nhóm; lời mời chưa trả lời bị hủy.</summary>
    [HttpPost("close")]
    [Authorize(Roles = Managers)]
    [ProducesResponseType(typeof(ApiResponse<TeamFormationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
        => Ok(ApiResponse<TeamFormationResponse>.Ok(await _service.CloseAsync(id, ct), "Đã kết thúc chọn nhóm"));

    /// <summary>Giảng viên đưa một sinh viên (không phải trưởng nhóm) ra khỏi nhóm, vd bạn nghỉ học.</summary>
    [HttpDelete("members/{studentProfileId:guid}")]
    [Authorize(Roles = Managers)]
    [ProducesResponseType(typeof(ApiResponse<TeamFormationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid studentProfileId, CancellationToken ct)
        => Ok(ApiResponse<TeamFormationResponse>.Ok(
            await _service.RemoveMemberAsync(id, studentProfileId, ct), "Đã đưa sinh viên ra khỏi nhóm"));

    /// <summary>
    /// Sau hạn chót: đề xuất xếp sinh viên chưa có nhóm và gộp các nhóm thiếu người (không lưu). Gửi lại qua POST assignments
    /// sau khi xem/sửa.
    /// </summary>
    [HttpGet("auto-assign/preview")]
    [Authorize(Roles = Managers)]
    [ProducesResponseType(typeof(ApiResponse<AutoAssignPreviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PreviewAutoAssign(Guid id, CancellationToken ct)
        => Ok(ApiResponse<AutoAssignPreviewResponse>.Ok(await _service.PreviewAutoAssignAsync(id, ct)));

    /// <summary>Xếp sinh viên chưa có nhóm vào nhóm có sẵn và/hoặc nhóm mới, gộp nhóm thiếu người (thủ công hoặc theo đề xuất tự động).</summary>
    [HttpPost("assignments")]
    [Authorize(Roles = Managers)]
    [ProducesResponseType(typeof(ApiResponse<TeamFormationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApplyAssignments(
        Guid id, [FromBody] ApplyTeamAssignmentsRequest request, CancellationToken ct)
        => Ok(ApiResponse<TeamFormationResponse>.Ok(
            await _service.ApplyAssignmentsAsync(id, request, ct), "Xếp nhóm thành công"));

    /// <summary>Chuyển một sinh viên đã có nhóm sang nhóm khác, khi đề tài của hai nhóm chưa được duyệt.</summary>
    [HttpPost("moves")]
    [Authorize(Roles = Managers)]
    [ProducesResponseType(typeof(ApiResponse<TeamFormationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MoveMember(Guid id, [FromBody] MoveTeamMemberRequest request, CancellationToken ct)
        => Ok(ApiResponse<TeamFormationResponse>.Ok(
            await _service.MoveMemberAsync(id, request, ct), "Chuyển nhóm thành công"));
}

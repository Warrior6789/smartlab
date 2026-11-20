using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.API.Filters;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Teamwork;
using SmartLab.BLL.Interfaces.Teamwork;

namespace SmartLab.API.Controllers.Teamwork;

/// <summary>
/// Chia nhóm của lớp: sinh viên tự lập nhóm (tới hạn chót nếu có); giảng viên đặt số thành viên, hạn chót và xếp / chuyển
/// sinh viên vào nhóm chưa có đề tài bất cứ lúc nào.
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

    /// <summary>Đặt số thành viên tối thiểu/tối đa mỗi nhóm và hạn chót (chỉ chặn thao tác của sinh viên).</summary>
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

    /// <summary>Đề xuất tự động xếp sinh viên chưa có nhóm (không lưu). Gửi lại qua POST assignments sau khi xem/sửa.</summary>
    [HttpGet("auto-assign/preview")]
    [Authorize(Roles = Managers)]
    [ProducesResponseType(typeof(ApiResponse<AutoAssignPreviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PreviewAutoAssign(Guid id, CancellationToken ct)
        => Ok(ApiResponse<AutoAssignPreviewResponse>.Ok(await _service.PreviewAutoAssignAsync(id, ct)));

    /// <summary>Xếp sinh viên chưa có nhóm vào nhóm có sẵn và/hoặc nhóm mới (thủ công hoặc theo đề xuất tự động).</summary>
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

    /// <summary>Chuyển một sinh viên đã có nhóm sang nhóm khác.</summary>
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

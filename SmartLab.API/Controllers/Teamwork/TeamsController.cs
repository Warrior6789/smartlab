using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Teamwork;
using SmartLab.BLL.Interfaces.Teamwork;

namespace SmartLab.API.Controllers.Teamwork;

[ApiController]
[Route("api")]
[Produces("application/json")]
[Authorize(Roles = RoleNames.Student)]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamsController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    /// <summary>Tạo nhóm trong lớp; sinh viên hiện tại tự động là trưởng nhóm.</summary>
    [HttpPost("classes/{classId:guid}/teams")]
    [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTeam(
        Guid classId, [FromBody] CreateTeamRequest request, CancellationToken ct)
    {
        var team = await _teamService.CreateTeamAsync(classId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<TeamResponse>.Ok(team, "Tạo nhóm thành công"));
    }

    /// <summary>Lấy nhóm đã tham gia của sinh viên hiện tại trong lớp.</summary>
    [HttpGet("classes/{classId:guid}/team")]
    [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyTeam(Guid classId, CancellationToken ct)
    {
        var team = await _teamService.GetMyTeamAsync(classId, ct);
        return Ok(ApiResponse<TeamResponse>.Ok(team));
    }

    /// <summary>Danh sách sinh viên cùng lớp mà trưởng nhóm có thể mời.</summary>
    [HttpGet("classes/{classId:guid}/team-candidates")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TeamCandidateResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCandidates(Guid classId, CancellationToken ct)
    {
        var candidates = await _teamService.GetCandidatesAsync(classId, ct);
        return Ok(ApiResponse<IReadOnlyList<TeamCandidateResponse>>.Ok(candidates));
    }

    /// <summary>Chi tiết nhóm dành cho trưởng nhóm và thành viên đã chấp nhận.</summary>
    [HttpGet("teams/{teamId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeam(Guid teamId, CancellationToken ct)
    {
        var team = await _teamService.GetTeamAsync(teamId, ct);
        return Ok(ApiResponse<TeamResponse>.Ok(team));
    }

    /// <summary>Trưởng nhóm gửi lời mời cho một sinh viên cùng lớp.</summary>
    [HttpPost("teams/{teamId:guid}/invitations")]
    [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InviteMember(
        Guid teamId, [FromBody] InviteTeamMemberRequest request, CancellationToken ct)
    {
        var team = await _teamService.InviteMemberAsync(teamId, request, ct);
        return Ok(ApiResponse<TeamResponse>.Ok(team, "Gửi lời mời thành công"));
    }

    /// <summary>Trưởng nhóm xóa lời mời chưa được chấp nhận.</summary>
    [HttpDelete("teams/{teamId:guid}/invitations/{studentProfileId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveUnacceptedMember(
        Guid teamId, Guid studentProfileId, CancellationToken ct)
    {
        var team = await _teamService.RemoveUnacceptedMemberAsync(teamId, studentProfileId, ct);
        return Ok(ApiResponse<TeamResponse>.Ok(team, "Xóa lời mời thành công"));
    }
}

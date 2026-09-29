using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Teamwork;
using SmartLab.BLL.Interfaces.Teamwork;

namespace SmartLab.API.Controllers.Teamwork;

[ApiController]
[Route("api/team-invitations")]
[Produces("application/json")]
[Authorize(Roles = RoleNames.Student)]
public class TeamInvitationsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamInvitationsController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    /// <summary>Danh sách lời mời đang chờ phản hồi của sinh viên hiện tại.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TeamInvitationResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyInvitations(CancellationToken ct)
    {
        var invitations = await _teamService.GetMyInvitationsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<TeamInvitationResponse>>.Ok(invitations));
    }

    /// <summary>Sinh viên hiện tại chấp nhận hoặc từ chối lời mời vào nhóm.</summary>
    [HttpPut("{teamId:guid}/response")]
    [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Respond(
        Guid teamId, [FromBody] RespondTeamInvitationRequest request, CancellationToken ct)
    {
        var team = await _teamService.RespondToInvitationAsync(teamId, request, ct);
        var message = request.Decision.Equals(TeamMemberStatuses.Accepted, StringComparison.OrdinalIgnoreCase)
            ? "Chấp nhận lời mời thành công"
            : "Từ chối lời mời thành công";
        return Ok(ApiResponse<TeamResponse>.Ok(team, message));
    }
}

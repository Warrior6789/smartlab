using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ProjectProposals;
using SmartLab.BLL.Interfaces.ProjectProposals;

namespace SmartLab.API.Controllers.ProjectProposals;

[ApiController]
[Route("api")]
[Produces("application/json")]
[Authorize(Roles = RoleNames.Student)]
public class ProjectProposalsController : ControllerBase
{
    private readonly IProjectProposalService _proposalService;

    public ProjectProposalsController(IProjectProposalService proposalService)
    {
        _proposalService = proposalService;
    }

    /// <summary>Trưởng nhóm tạo đề tài, chọn số lượng linh kiện và gửi giảng viên duyệt.</summary>
    [HttpPost("teams/{teamId:guid}/project-proposals")]
    [Authorize(Policy = PermissionCodes.ProjectCreate)]
    [Authorize(Policy = PermissionCodes.BorrowCreate)]
    [ProducesResponseType(typeof(ApiResponse<ProjectProposalResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        Guid teamId, [FromBody] SubmitProjectProposalRequest request, CancellationToken ct)
    {
        var proposal = await _proposalService.SubmitAsync(teamId, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<ProjectProposalResponse>.Ok(proposal, "Gửi đề tài thành công"));
    }

    /// <summary>Trưởng nhóm hoặc thành viên đã chấp nhận xem đề tài của nhóm.</summary>
    [HttpGet("teams/{teamId:guid}/project-proposal")]
    [ProducesResponseType(typeof(ApiResponse<ProjectProposalResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByTeam(Guid teamId, CancellationToken ct)
    {
        var proposal = await _proposalService.GetByTeamAsync(teamId, ct);
        return Ok(ApiResponse<ProjectProposalResponse>.Ok(proposal));
    }

    /// <summary>Trưởng nhóm sửa đề tài bị từ chối và gửi lại để duyệt.</summary>
    [HttpPut("project-proposals/{projectId:guid}/resubmit")]
    [Authorize(Policy = PermissionCodes.ProjectCreate)]
    [Authorize(Policy = PermissionCodes.BorrowCreate)]
    [ProducesResponseType(typeof(ApiResponse<ProjectProposalResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resubmit(
        Guid projectId, [FromBody] SubmitProjectProposalRequest request, CancellationToken ct)
    {
        var proposal = await _proposalService.ResubmitAsync(projectId, request, ct);
        return Ok(ApiResponse<ProjectProposalResponse>.Ok(proposal, "Gửi lại đề tài thành công"));
    }
}

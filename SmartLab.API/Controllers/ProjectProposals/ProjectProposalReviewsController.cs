using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ProjectProposals;
using SmartLab.BLL.Interfaces.ProjectProposals;

namespace SmartLab.API.Controllers.ProjectProposals;

[ApiController]
[Route("api/project-proposal-reviews")]
[Produces("application/json")]
[Authorize(Policy = PermissionCodes.ProjectProposalApprove)]
public class ProjectProposalReviewsController : ControllerBase
{
    private readonly IProjectProposalReviewService _reviewService;

    public ProjectProposalReviewsController(IProjectProposalReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>Danh sách proposal Pending thuộc các lớp giảng viên hiện tại phụ trách.</summary>
    [HttpGet("assigned-to-me")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProjectProposalReviewItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAssigned([FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _reviewService.GetAssignedAsync(paging, ct);
        return Ok(ApiResponse<PagedResult<ProjectProposalReviewItemResponse>>.Ok(result));
    }

    /// <summary>Chi tiết đề tài và danh sách linh kiện để giảng viên review.</summary>
    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectProposalReviewDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid projectId, CancellationToken ct)
    {
        var proposal = await _reviewService.GetDetailAsync(projectId, ct);
        return Ok(ApiResponse<ProjectProposalReviewDetailResponse>.Ok(proposal));
    }

    /// <summary>Giảng viên phụ trách lớp phê duyệt hoặc từ chối proposal.</summary>
    [HttpPut("{projectId:guid}/decision")]
    [ProducesResponseType(typeof(ApiResponse<ProjectProposalReviewDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Review(
        Guid projectId, [FromBody] ReviewProjectProposalRequest request, CancellationToken ct)
    {
        var proposal = await _reviewService.ReviewAsync(projectId, request, ct);
        var message = request.Decision.Equals(ProjectStatuses.Approved, StringComparison.OrdinalIgnoreCase)
            ? "Phê duyệt đề tài thành công"
            : "Từ chối đề tài thành công";
        return Ok(ApiResponse<ProjectProposalReviewDetailResponse>.Ok(proposal, message));
    }
}

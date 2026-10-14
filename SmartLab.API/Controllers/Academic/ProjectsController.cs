using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartLab.API.Extensions;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.Interfaces.Academic;

namespace SmartLab.API.Controllers.Academic;

[ApiController]
[Route("api/projects")]
[Produces("application/json")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IComponentSuggestionService _suggestionService;
    private readonly IProjectReviewService _reviewService;

    public ProjectsController(
        IProjectService projectService, IComponentSuggestionService suggestionService, IProjectReviewService reviewService)
    {
        _projectService = projectService;
        _suggestionService = suggestionService;
        _reviewService = reviewService;
    }

    /// <summary>
    /// Project đang chờ mình duyệt (đã đến lượt: các bước trước đã duyệt xong). Admin thấy mọi project đang chờ.
    /// Tìm theo tên project, tên nhóm, mã đơn mượn. Gửi sớm nhất lên trước.
    /// </summary>
    [HttpGet("pending-approval")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PendingApprovalProjectResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingApproval(
        [FromQuery] PendingApprovalQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _reviewService.GetPendingAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<PendingApprovalProjectResponse>>.Ok(result));
    }

    /// <summary>
    /// Giảng viên của lớp duyệt (Admin duyệt thay được), kể cả đơn có HIGH_VALUE.
    /// Duyệt xong thì project và đơn mượn thành Approved.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveProject(Guid id, [FromBody] ApproveProjectRequest request, CancellationToken ct)
    {
        var project = await _reviewService.ApproveAsync(id, request, ct);
        return Ok(ApiResponse<ProjectDetailResponse>.Ok(project, "Duyệt project thành công"));
    }

    /// <summary>Từ chối (bắt buộc ghi lý do): project và đơn mượn thành Rejected, nhóm sửa rồi gửi duyệt lại.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectProject(Guid id, [FromBody] RejectProjectRequest request, CancellationToken ct)
    {
        var project = await _reviewService.RejectAsync(id, request, ct);
        return Ok(ApiResponse<ProjectDetailResponse>.Ok(project, "Đã từ chối project"));
    }

    /// <summary>
    /// Danh sách project (phân trang, tìm theo tên project/nhóm, lọc theo lớp, trạng thái).
    /// Admin thấy tất cả; sinh viên thấy project nhóm mình; giảng viên thấy project các lớp mình dạy và các đơn mình duyệt.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProjectResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProjects(
        [FromQuery] ProjectQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _projectService.GetProjectsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<ProjectResponse>>.Ok(result));
    }

    /// <summary>Chi tiết project: linh kiện cần dùng, nhận xét của giảng viên, các đơn mượn và tiến độ duyệt.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProject(Guid id, CancellationToken ct)
    {
        var project = await _projectService.GetProjectByIdAsync(id, ct);
        return Ok(ApiResponse<ProjectDetailResponse>.Ok(project));
    }

    /// <summary>Nhóm trưởng lưu nháp project của nhóm (trạng thái Draft). Mỗi nhóm chỉ có 1 project.</summary>
    [HttpPost("/api/teams/{teamId:guid}/project")]
    [Authorize(Policy = PermissionCodes.ProjectCreate)]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProject(Guid teamId, [FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var project = await _projectService.CreateProjectAsync(teamId, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ProjectDetailResponse>.Ok(project, "Lưu nháp project thành công"));
    }

    /// <summary>
    /// AI đọc mô tả project và gợi ý linh kiện, chia 3 nhóm: mượn được ở lab đã chọn, kho có nhưng lab không đủ,
    /// và món tự mua (kho không có). Không lưu gì: nhóm trưởng chọn món rồi lưu bằng PUT /api/projects/{id}.
    /// Mỗi tài khoản tối đa 10 lần / giờ (quá thì 429).
    /// </summary>
    [HttpPost("suggest-components")]
    [Authorize(Policy = PermissionCodes.ProjectCreate)]
    [EnableRateLimiting(RateLimitExtensions.AiSuggestionPolicy)]
    [ProducesResponseType(typeof(ApiResponse<ComponentSuggestionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SuggestComponents([FromBody] SuggestComponentsRequest request, CancellationToken ct)
    {
        var suggestion = await _suggestionService.SuggestAsync(request, ct);
        return Ok(ApiResponse<ComponentSuggestionResponse>.Ok(suggestion));
    }

    /// <summary>Nhóm trưởng sửa tên, mô tả, toàn bộ danh sách linh kiện mượn kho và món tự mua (chỉ khi Draft hoặc Rejected).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ProjectCreate)]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await _projectService.UpdateProjectAsync(id, request, ct);
        return Ok(ApiResponse<ProjectDetailResponse>.Ok(project, "Sửa project thành công"));
    }

    /// <summary>Nhóm trưởng cập nhật link GitHub, slide, video demo (được sửa ở mọi trạng thái).</summary>
    [HttpPut("{id:guid}/links")]
    [Authorize(Policy = PermissionCodes.ProjectCreate)]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProjectLinks(Guid id, [FromBody] UpdateProjectLinksRequest request, CancellationToken ct)
    {
        var project = await _projectService.UpdateProjectLinksAsync(id, request, ct);
        return Ok(ApiResponse<ProjectDetailResponse>.Ok(project, "Cập nhật link thành công"));
    }

    /// <summary>
    /// Gửi duyệt: project thành Submitted và tạo luôn đơn mượn (Pending) từ danh sách linh kiện ở lab đã chọn.
    /// Người duyệt là giảng viên của lớp, kể cả khi có thiết bị HIGH_VALUE.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = PermissionCodes.ProjectCreate)]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitProject(Guid id, [FromBody] SubmitProjectRequest request, CancellationToken ct)
    {
        var project = await _projectService.SubmitProjectAsync(id, request, ct);
        return Ok(ApiResponse<ProjectDetailResponse>.Ok(project, "Gửi duyệt project thành công"));
    }

    /// <summary>
    /// Nhóm trưởng rút lại project đang chờ duyệt (chưa ai quyết định xong): đơn mượn đang chờ bị hủy, project về Draft để sửa tiếp.
    /// </summary>
    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Policy = PermissionCodes.ProjectCreate)]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WithdrawProject(Guid id, CancellationToken ct)
    {
        var project = await _projectService.WithdrawProjectAsync(id, ct);
        return Ok(ApiResponse<ProjectDetailResponse>.Ok(project, "Đã rút lại project"));
    }
}

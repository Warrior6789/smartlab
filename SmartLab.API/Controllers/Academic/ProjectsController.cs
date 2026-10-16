using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.Interfaces.Academic;

namespace SmartLab.API.Controllers.Academic;

[ApiController]
[Route("api/projects")]
[Produces("application/json")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    /// <summary>Nhóm trưởng nộp đề tài môn học kèm danh sách linh kiện xin mượn.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProjectResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitProject([FromBody] SubmitProjectRequest request, CancellationToken ct)
    {
        var result = await _projectService.SubmitProjectAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ProjectResponse>.Ok(result, "Đăng ký đề tài thành công"));
    }

    /// <summary>Giảng viên hoặc Admin duyệt đề tài và chốt danh sách linh kiện mượn.</summary>
    [HttpPut("{id:guid}/review")]
    [ProducesResponseType(typeof(ApiResponse<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReviewProject(Guid id, [FromBody] ReviewProjectRequest request, CancellationToken ct)
    {
        var result = await _projectService.ReviewProjectAsync(id, request, ct);
        var msg = request.Approve ? "Đã phê duyệt đề tài thành công" : "Đã từ chối đề tài";
        return Ok(ApiResponse<ProjectResponse>.Ok(result, msg));
    }

    /// <summary>Xem danh sách đề tài của tất cả các nhóm trong 1 lớp học.</summary>
    [HttpGet("classes/{classId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<List<ProjectResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectsByClass(Guid classId, CancellationToken ct)
    {
        var result = await _projectService.GetProjectsByClassAsync(classId, ct);
        return Ok(ApiResponse<List<ProjectResponse>>.Ok(result));
    }

    /// <summary>Xem chi tiết 1 đề tài kèm danh sách linh kiện và ý kiến giảng viên.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectById(Guid id, CancellationToken ct)
    {
        var result = await _projectService.GetProjectByIdAsync(id, ct);
        return Ok(ApiResponse<ProjectResponse>.Ok(result));
    }

    /// <summary>Lấy thông tin đề tài của 1 nhóm cụ thể.</summary>
    [HttpGet("teams/{teamId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectByTeam(Guid teamId, CancellationToken ct)
    {
        var result = await _projectService.GetProjectByTeamAsync(teamId, ct);
        return Ok(ApiResponse<ProjectResponse?>.Ok(result));
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.Interfaces.Academic;

namespace SmartLab.API.Controllers.Academic;

[ApiController]
[Route("api/courses")]
[Produces("application/json")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService)
        => _courseService = courseService;

    [HttpPost]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<CourseResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request, CancellationToken ct)
    {
        var result = await _courseService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CourseResponse>.Ok(result, "Tạo môn học thành công"));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CourseResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(ApiResponse<List<CourseResponse>>.Ok(await _courseService.GetAllAsync(ct)));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CourseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Ok(ApiResponse<CourseResponse>.Ok(await _courseService.GetByIdAsync(id, ct)));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<CourseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseRequest request, CancellationToken ct)
    {
        var result = await _courseService.UpdateAsync(id, request, ct);
        return Ok(ApiResponse<CourseResponse>.Ok(result, "Cập nhật môn học thành công"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _courseService.DeleteAsync(id, ct);
        return NoContent();
    }
}
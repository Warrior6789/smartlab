using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.Interfaces.Academic;

namespace SmartLab.API.Controllers.Academic;

[ApiController]
[Route("api/classes")]
[Produces("application/json")]
[Authorize]
public class ClassesController : ControllerBase
{
    private readonly IClassService _classService;
    private readonly IClassRosterService _rosterService;

    public ClassesController(IClassService classService, IClassRosterService rosterService)
    {
        _classService = classService;
        _rosterService = rosterService;
    }

    [HttpPost]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<ClassResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateClassRequest request, CancellationToken ct)
    {
        var result = await _classService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ClassResponse>.Ok(result, "Tạo lớp học thành công"));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ClassResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? semesterId, CancellationToken ct)
        => Ok(ApiResponse<List<ClassResponse>>.Ok(await _classService.GetAllAsync(semesterId, ct)));

    /// <summary>Lớp của người đang đăng nhập: sinh viên thấy lớp mình đang học (Studying), giảng viên thấy lớp mình dạy.</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<List<ClassResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyClasses([FromQuery] Guid? semesterId, CancellationToken ct)
        => Ok(ApiResponse<List<ClassResponse>>.Ok(await _classService.GetMyClassesAsync(semesterId, ct)));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ClassResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Ok(ApiResponse<ClassResponse>.Ok(await _classService.GetByIdAsync(id, ct)));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<ClassResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClassRequest request, CancellationToken ct)
    {
        var result = await _classService.UpdateAsync(id, request, ct);
        return Ok(ApiResponse<ClassResponse>.Ok(result, "Cập nhật lớp học thành công"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _classService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Import danh sách sinh viên từ Excel.</summary>
    [HttpPost("{id:guid}/roster")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ImportRosterResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImportRoster(Guid id, [FromForm] ImportRosterRequest request, CancellationToken ct)
    {
        var result = await _rosterService.ImportStudentsAsync(id, request, ct);
        return Ok(ApiResponse<ImportRosterResult>.Ok(result, "Import hoàn tất"));
    }
    /// <summary>Phân công giảng viên vào lớp.</summary>
    [HttpPost("assign-instructor")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<AssignInstructorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignInstructor([FromBody] AssignInstructorRequest request, CancellationToken ct)
    {
        var result = await _rosterService.AssignInstructorAsync(request, ct);
        return Ok(ApiResponse<AssignInstructorResponse>.Ok(result, "Phân công giảng viên thành công"));
    }
}
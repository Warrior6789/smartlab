using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.DTOs.Academic;

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
    /// <summary>Danh sách sinh viên của lớp. Admin hoặc giảng viên phụ trách chính lớp đó.</summary>
    [HttpGet("{classId:guid}/students")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ClassStudentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudents(
        Guid classId, [FromQuery] ClassStudentQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _rosterService.GetStudentsAsync(classId, query, paging, ct);
        return Ok(ApiResponse<PagedResult<ClassStudentResponse>>.Ok(result));
    }

    /// <summary>Thêm 1 sinh viên vào lớp; chưa có tài khoản thì tạo và gửi mật khẩu qua email như roster import.</summary>
    [HttpPost("{classId:guid}/students")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<ClassStudentResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddStudent(Guid classId, [FromBody] AddClassStudentRequest request, CancellationToken ct)
    {
        var result = await _rosterService.AddStudentAsync(classId, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ClassStudentResponse>.Ok(result, "Thêm sinh viên vào lớp thành công"));
    }

    /// <summary>Đổi trạng thái sinh viên trong lớp (Studying/Dropped).</summary>
    [HttpPut("{classId:guid}/students/{studentProfileId:guid}/status")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<ClassStudentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStudentStatus(
        Guid classId, Guid studentProfileId, [FromBody] UpdateClassStudentStatusRequest request, CancellationToken ct)
    {
        var result = await _rosterService.UpdateStudentStatusAsync(classId, studentProfileId, request, ct);
        return Ok(ApiResponse<ClassStudentResponse>.Ok(result, "Cập nhật trạng thái sinh viên thành công"));
    }

    /// <summary>Xóa sinh viên khỏi lớp (sửa import nhầm). Chỉ xóa được khi chưa dính tới nhóm nào trong lớp.</summary>
    [HttpDelete("{classId:guid}/students/{studentProfileId:guid}")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveStudent(Guid classId, Guid studentProfileId, CancellationToken ct)
    {
        await _rosterService.RemoveStudentAsync(classId, studentProfileId, ct);
        return NoContent();
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
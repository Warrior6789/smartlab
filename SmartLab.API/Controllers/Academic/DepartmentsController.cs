using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.Interfaces.Academic;

namespace SmartLab.API.Controllers.Academic;

[ApiController]
[Route("api/departments")]
[Produces("application/json")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    /// <summary>Danh sách bộ môn có tìm kiếm và phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DepartmentListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDepartments(
        [FromQuery] DepartmentQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _departmentService.GetDepartmentsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<DepartmentListItemResponse>>.Ok(result));
    }

    /// <summary>Chi tiết bộ môn và danh sách giảng viên.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDepartment(Guid id, CancellationToken ct)
    {
        var result = await _departmentService.GetDepartmentByIdAsync(id, ct);
        return Ok(ApiResponse<DepartmentDetailResponse>.Ok(result));
    }

    /// <summary>Tạo bộ môn.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDepartment(
        [FromBody] CreateDepartmentRequest request, CancellationToken ct)
    {
        var result = await _departmentService.CreateDepartmentAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<DepartmentDetailResponse>.Ok(result, "Tạo bộ môn thành công"));
    }

    /// <summary>Đổi tên bộ môn.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDepartment(
        Guid id, [FromBody] UpdateDepartmentRequest request, CancellationToken ct)
    {
        var result = await _departmentService.UpdateDepartmentAsync(id, request, ct);
        return Ok(ApiResponse<DepartmentDetailResponse>.Ok(result, "Cập nhật bộ môn thành công"));
    }

    /// <summary>Gán, đổi hoặc gỡ Trưởng bộ môn.</summary>
    [HttpPut("{id:guid}/head")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDepartmentHead(
        Guid id, [FromBody] SetDepartmentHeadRequest request, CancellationToken ct)
    {
        var result = await _departmentService.SetDepartmentHeadAsync(id, request, ct);
        return Ok(ApiResponse<DepartmentDetailResponse>.Ok(result.Department, result.Message));
    }

    /// <summary>Thêm hoặc chuyển giảng viên vào bộ môn.</summary>
    [HttpPost("{id:guid}/lecturers")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddLecturer(
        Guid id, [FromBody] AddDepartmentLecturerRequest request, CancellationToken ct)
    {
        var result = await _departmentService.AddLecturerAsync(id, request, ct);
        return Ok(ApiResponse<DepartmentDetailResponse>.Ok(result, "Thêm hoặc chuyển giảng viên thành công"));
    }

    /// <summary>Gỡ giảng viên khỏi bộ môn.</summary>
    [HttpDelete("{id:guid}/lecturers/{lecturerId:guid}")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveLecturer(Guid id, Guid lecturerId, CancellationToken ct)
    {
        var result = await _departmentService.RemoveLecturerAsync(id, lecturerId, ct);
        return Ok(ApiResponse<DepartmentDetailResponse>.Ok(result, "Gỡ giảng viên khỏi bộ môn thành công"));
    }
}

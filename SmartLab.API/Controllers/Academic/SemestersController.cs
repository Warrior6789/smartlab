using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.Interfaces.Academic;

namespace SmartLab.API.Controllers.Academic;

[ApiController]
[Route("api/semesters")]
[Produces("application/json")]
[Authorize]
public class SemestersController : ControllerBase
{
    private readonly ISemesterService _semesterService;

    public SemestersController(ISemesterService semesterService)
        => _semesterService = semesterService;

    /// <summary>Tạo kỳ học mới.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<SemesterResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateSemesterRequest request, CancellationToken ct)
    {
        var result = await _semesterService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<SemesterResponse>.Ok(result, "Tạo kỳ học thành công"));
    }

    /// <summary>Lấy danh sách tất cả kỳ học.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<SemesterResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _semesterService.GetAllAsync(ct);
        return Ok(ApiResponse<List<SemesterResponse>>.Ok(result));
    }

    /// <summary>Lấy chi tiết một kỳ học.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SemesterResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _semesterService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<SemesterResponse>.Ok(result));
    }

    /// <summary>Cập nhật kỳ học.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(typeof(ApiResponse<SemesterResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSemesterRequest request, CancellationToken ct)
    {
        var result = await _semesterService.UpdateAsync(id, request, ct);
        return Ok(ApiResponse<SemesterResponse>.Ok(result, "Cập nhật kỳ học thành công"));
    }

    /// <summary>Xóa kỳ học (chỉ khi không có lớp).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionCodes.AcademicManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _semesterService.DeleteAsync(id, ct);
        return NoContent();
    }
}
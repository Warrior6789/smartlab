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

    public ClassesController(IClassService classService)
        => _classService = classService;

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
}
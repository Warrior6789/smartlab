using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/components/{componentId:guid}/tutorials")]
[Produces("application/json")]
public class ComponentTutorialsController : ControllerBase
{
    private readonly ITutorialService _tutorialService;

    public ComponentTutorialsController(ITutorialService tutorialService)
    {
        _tutorialService = tutorialService;
    }

    /// <summary>Danh sách bài hướng dẫn của linh kiện, mới nhất trước.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ComponentView)]
    [ProducesResponseType(typeof(ApiResponse<List<TutorialResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(Guid componentId, CancellationToken ct)
    {
        var tutorials = await _tutorialService.GetByComponentAsync(componentId, ct);
        return Ok(ApiResponse<List<TutorialResponse>>.Ok(tutorials));
    }

    /// <summary>Viết bài hướng dẫn mới; người viết là người đang đăng nhập.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionCodes.TutorialManage)]
    [ProducesResponseType(typeof(ApiResponse<TutorialResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid componentId, [FromBody] TutorialRequest request, CancellationToken ct)
    {
        var tutorial = await _tutorialService.CreateAsync(componentId, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<TutorialResponse>.Ok(tutorial, "Tạo bài hướng dẫn thành công"));
    }

    /// <summary>Sửa bài hướng dẫn (chỉ người viết hoặc Admin).</summary>
    [HttpPut("{tutorialId:guid}")]
    [Authorize(Policy = PermissionCodes.TutorialManage)]
    [ProducesResponseType(typeof(ApiResponse<TutorialResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid componentId, Guid tutorialId, [FromBody] TutorialRequest request, CancellationToken ct)
    {
        var tutorial = await _tutorialService.UpdateAsync(componentId, tutorialId, request, ct);
        return Ok(ApiResponse<TutorialResponse>.Ok(tutorial, "Cập nhật bài hướng dẫn thành công"));
    }

    /// <summary>Xóa hẳn bài hướng dẫn (chỉ người viết hoặc Admin).</summary>
    [HttpDelete("{tutorialId:guid}")]
    [Authorize(Policy = PermissionCodes.TutorialManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid componentId, Guid tutorialId, CancellationToken ct)
    {
        await _tutorialService.DeleteAsync(componentId, tutorialId, ct);
        return Ok(ApiResponse.Ok("Xóa bài hướng dẫn thành công"));
    }
}

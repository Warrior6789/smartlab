using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/components/{componentId:guid}/images")]
[Produces("application/json")]
public class ComponentImagesController : ControllerBase
{
    private readonly IComponentImageService _componentImageService;

    public ComponentImagesController(IComponentImageService componentImageService)
    {
        _componentImageService = componentImageService;
    }

    /// <summary>Tải ảnh lên (JPG/PNG/WEBP, tối đa 5 MB). Ảnh đầu tiên tự thành ảnh chính.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ComponentImageResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadImage(Guid componentId, [FromForm] UploadComponentImageRequest request, CancellationToken ct)
    {
        var image = await _componentImageService.UploadImageAsync(componentId, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ComponentImageResponse>.Ok(image, "Tải ảnh thành công"));
    }

    /// <summary>Xóa ảnh (xóa cả file trên kho lưu trữ).</summary>
    [HttpDelete("{imageId:guid}")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(Guid componentId, Guid imageId, CancellationToken ct)
    {
        await _componentImageService.DeleteImageAsync(componentId, imageId, ct);
        return Ok(ApiResponse.Ok("Xóa ảnh thành công"));
    }
}

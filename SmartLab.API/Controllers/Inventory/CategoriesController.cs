using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;

namespace SmartLab.API.Controllers.Inventory;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>Danh sách danh mục linh kiện.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.ComponentView)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var categories = await _categoryService.GetCategoriesAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<CategoryResponse>>.Ok(categories));
    }

    /// <summary>Tạo danh mục linh kiện.</summary>
    [HttpPost]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var category = await _categoryService.CreateCategoryAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CategoryResponse>.Ok(category, "Tạo danh mục thành công"));
    }

    /// <summary>Sửa danh mục linh kiện.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        var category = await _categoryService.UpdateCategoryAsync(id, request, ct);
        return Ok(ApiResponse<CategoryResponse>.Ok(category, "Cập nhật danh mục thành công"));
    }

    /// <summary>Xóa danh mục chưa có linh kiện nào.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ComponentManage)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        await _categoryService.DeleteCategoryAsync(id, ct);
        return Ok(ApiResponse.Ok("Xóa danh mục thành công"));
    }
}

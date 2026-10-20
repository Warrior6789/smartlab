using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface ICategoryService
{
    /// <summary>All component categories with their component counts.</summary>
    Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken ct = default);

    /// <summary>Creates a category; name must be unique (case-insensitive).</summary>
    Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default);

    /// <summary>Renames a category; name must stay unique (case-insensitive).</summary>
    Task<CategoryResponse> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequest request, CancellationToken ct = default);

    /// <summary>Deletes a category that has no components.</summary>
    Task DeleteCategoryAsync(Guid categoryId, CancellationToken ct = default);
}

using SmartLab.BLL.DTOs.Inventory;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Inventory;

public static class CategoryMappings
{
    /// <summary>Requires Components to be loaded (empty for a new category).</summary>
    public static CategoryResponse ToCategoryResponse(ComponentCategory category) => new()
    {
        CategoryId = category.CategoryId,
        CategoryName = category.CategoryName,
        Description = category.Description,
        ComponentCount = category.Components.Count,
    };
}

namespace SmartLab.BLL.DTOs.Inventory;

public class UpdateCategoryRequest
{
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

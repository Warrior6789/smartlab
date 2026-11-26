namespace SmartLab.BLL.DTOs.Inventory;

public class ComponentSpecRequest
{
    public string SpecKey { get; set; } = string.Empty;
    public string SpecValue { get; set; } = string.Empty;
    public string? Unit { get; set; }
}

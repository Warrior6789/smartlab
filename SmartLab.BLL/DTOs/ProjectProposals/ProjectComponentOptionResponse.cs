namespace SmartLab.BLL.DTOs.ProjectProposals;

public class ProjectComponentOptionResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public int AvailableQuantity { get; set; }
}

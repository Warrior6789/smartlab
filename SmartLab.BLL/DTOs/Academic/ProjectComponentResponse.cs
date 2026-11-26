namespace SmartLab.BLL.DTOs.Academic;

public class ProjectComponentResponse
{
    public Guid ComponentId { get; set; }

    public string ComponentCode { get; set; } = string.Empty;

    public string ComponentName { get; set; } = string.Empty;

    public string TrackingType { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? Note { get; set; }
}

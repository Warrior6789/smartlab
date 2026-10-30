namespace SmartLab.BLL.DTOs.Reports;

public class TopComponentResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public string Unit { get; set; } = null!;

    /// <summary>Number of borrow requests that took this component.</summary>
    public int TimesBorrowed { get; set; }

    public int TotalQuantity { get; set; }
}

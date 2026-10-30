namespace SmartLab.BLL.DTOs.Reports;

public class LowStockResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public string TrackingType { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public int Available { get; set; }
    public int MinStock { get; set; }

    /// <summary>MinStock - Available: how many to buy to reach the minimum.</summary>
    public int Shortage { get; set; }

    /// <summary>Unit price on the latest completed stock receipt; null when never received or for LabStaff.</summary>
    public decimal? LastUnitPrice { get; set; }
}

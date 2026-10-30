namespace SmartLab.BLL.DTOs.Reports;

public class ClassUsageResponse
{
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = null!;

    /// <summary>Requests with at least one line issued in the period.</summary>
    public int RequestCount { get; set; }

    public int IssuedQuantity { get; set; }
    public int ReturnedQuantity { get; set; }

    /// <summary>Still issued, not returned yet.</summary>
    public int HoldingQuantity { get; set; }

    public int DamagedUnits { get; set; }
    public int LostUnits { get; set; }
}

namespace SmartLab.BLL.DTOs.Reports;

public class ClassUsageResponse
{
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = null!;

    /// <summary>Requests with at least one line issued in the period.</summary>
    public int RequestCount { get; set; }

    public int IssuedQuantity { get; set; }
    /// <summary>Returned in the period.</summary>
    public int ReturnedQuantity { get; set; }

    /// <summary>Issued and not returned yet, right now, whatever the period.</summary>
    public int HoldingQuantity { get; set; }

    /// <summary>Lines returned damaged in the period.</summary>
    public int DamagedUnits { get; set; }
    /// <summary>Lines reported lost in the period.</summary>
    public int LostUnits { get; set; }
}

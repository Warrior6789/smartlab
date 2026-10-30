namespace SmartLab.BLL.DTOs.Reports;

public class LossLineResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public string TrackingType { get; set; } = null!;

    /// <summary>Units that became Lost in the period.</summary>
    public int LostUnits { get; set; }

    /// <summary>Units that became Retired (damaged, end of life) in the period.</summary>
    public int RetiredUnits { get; set; }

    /// <summary>Consumable quantity taken off by negative adjustments (stock counts, corrections).</summary>
    public int AdjustedOutQuantity { get; set; }

    /// <summary>Units at purchase price + adjusted quantity at the latest receipt price.</summary>
    public decimal Value { get; set; }
}

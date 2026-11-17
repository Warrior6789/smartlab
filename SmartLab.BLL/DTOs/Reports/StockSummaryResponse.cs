namespace SmartLab.BLL.DTOs.Reports;

public class StockSummaryResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public string TrackingType { get; set; } = null!;
    public string Unit { get; set; } = null!;

    /// <summary>In the cabinets: consumable quantity, or units with status Available.</summary>
    public int Available { get; set; }

    /// <summary>Units currently lent out (always 0 for consumables).</summary>
    public int Borrowed { get; set; }

    /// <summary>Units with status Lost or Retired (no longer in stock).</summary>
    public int LostOrRetired { get; set; }

    public int? MinStock { get; set; }

    /// <summary>
    /// Available + Borrowed is below MinStock. Lent units come back, so lending alone never makes a component
    /// low; only consumables used up and units lost or retired do.
    /// </summary>
    public bool IsLowStock { get; set; }
}

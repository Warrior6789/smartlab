namespace SmartLab.BLL.DTOs.Reports;

public class StockSummaryQueryParams
{
    /// <summary>Only stock in this lab; LabStaff may only pass a lab they are assigned to.</summary>
    public Guid? RoomId { get; set; }

    public Guid? CategoryId { get; set; }

    /// <summary>CONSUMABLE, REUSABLE or HIGH_VALUE.</summary>
    public string? TrackingType { get; set; }

    /// <summary>Matches component code or name.</summary>
    public string? Search { get; set; }
}

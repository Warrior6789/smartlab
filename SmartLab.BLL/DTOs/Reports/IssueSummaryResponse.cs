namespace SmartLab.BLL.DTOs.Reports;

public class IssueSummaryResponse
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>Issue reports created in the period.</summary>
    public int TotalIssues { get; set; }

    public List<CountGroupResponse> ByStatus { get; set; } = new();

    /// <summary>Damaged, Burnt or Lost.</summary>
    public List<CountGroupResponse> ByType { get; set; } = new();

    /// <summary>Component codes with the most reports (top 10).</summary>
    public List<CountGroupResponse> TopComponents { get; set; } = new();

    /// <summary>Average hours from report to resolution; null when none was resolved.</summary>
    public double? AverageResolveHours { get; set; }
}

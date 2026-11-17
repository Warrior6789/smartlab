namespace SmartLab.BLL.DTOs.Reports;

public class BorrowSummaryResponse
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>Requests created in the period.</summary>
    public int TotalRequests { get; set; }

    public List<CountGroupResponse> ByStatus { get; set; } = new();

    /// <summary>By pickup lab code.</summary>
    public List<CountGroupResponse> ByRoom { get; set; } = new();

    /// <summary>By class code (requests without a class are left out).</summary>
    public List<CountGroupResponse> ByClass { get; set; } = new();

    /// <summary>Rejected / (approved + rejected) in percent; null when nothing was decided.</summary>
    public double? RejectionRate { get; set; }

    /// <summary>Average hours from approval request to decision; null when nothing was decided.</summary>
    public double? AverageApprovalHours { get; set; }
}

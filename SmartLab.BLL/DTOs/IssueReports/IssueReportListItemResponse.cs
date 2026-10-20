namespace SmartLab.BLL.DTOs.IssueReports;

public class IssueReportListItemResponse
{
    public Guid IssueReportId { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid ComponentItemId { get; set; }
    public string? ItemCode { get; set; }
    public Guid? ComponentId { get; set; }
    public string? ComponentCode { get; set; }
    public string? ComponentName { get; set; }
    public Guid? RoomId { get; set; }
    public string? RoomCode { get; set; }
    public Guid ReportedById { get; set; }
    public IssueReportActorResponse? ReportedBy { get; set; }
}
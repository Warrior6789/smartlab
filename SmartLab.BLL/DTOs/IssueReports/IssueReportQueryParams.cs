namespace SmartLab.BLL.DTOs.IssueReports;

public class IssueReportQueryParams
{
    public string? Status { get; set; }
    public string? IssueType { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? ComponentId { get; set; }
    public Guid? ComponentItemId { get; set; }
    public string? ItemCode { get; set; }
    public Guid? ReportedBy { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool? Mine { get; set; }
}
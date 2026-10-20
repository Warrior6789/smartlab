namespace SmartLab.BLL.DTOs.IssueReports;

public class IssueReportDetailResponse : IssueReportListItemResponse
{
    public Guid? BorrowItemId { get; set; }
    public Guid? ResolvedById { get; set; }
    public IssueReportActorResponse? ResolvedBy { get; set; }
    public IssueReportItemResponse? Item { get; set; }
    public IssueReportBorrowContextResponse? BorrowContext { get; set; }
}
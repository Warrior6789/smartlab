namespace SmartLab.BLL.DTOs.IssueReports;

public class IssueReportDetailResponse : IssueReportListItemResponse
{
    public Guid? BorrowItemId { get; set; }
    public Guid? ResolvedById { get; set; }
    public IssueReportActorResponse? ResolvedBy { get; set; }
    public IssueReportItemResponse? Item { get; set; }
    public IssueReportBorrowContextResponse? BorrowContext { get; set; }

    /// <summary>Only in the create response: the replacement line added to the borrow request, if any.</summary>
    public Guid? ReplacementBorrowItemId { get; set; }
}
namespace SmartLab.BLL.DTOs.IssueReports;

public class ConfirmBorrowingReturnIssueResult
{
    public Guid IssueReportId { get; set; }
    public bool ReusedOpenReport { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

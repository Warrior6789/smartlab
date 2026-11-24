namespace SmartLab.BLL.DTOs.Borrowing;

public class RecordLoanIssueResult
{
    public Guid IssueReportId { get; set; }

    /// <summary>The Pending replacement line; null when none was asked for or the lab has no free unit left.</summary>
    public Guid? ReplacementBorrowItemId { get; set; }
}

namespace SmartLab.BLL.DTOs.IssueReports;

/// <summary>
/// Internal command used by Borrowing while receiving a damaged, burnt or lost item.
/// The Borrowing caller owns the surrounding database transaction.
/// </summary>
public class ConfirmBorrowingReturnIssueCommand
{
    public Guid BorrowItemId { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

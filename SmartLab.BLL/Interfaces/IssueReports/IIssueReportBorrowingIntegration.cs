using SmartLab.BLL.DTOs.IssueReports;

namespace SmartLab.BLL.Interfaces.IssueReports;

/// <summary>
/// Internal integration contract for the Borrowing return workflow.
/// The caller must start, commit and roll back a transaction through the same scoped
/// IUnitOfWork used by this service. This service never owns that transaction.
/// </summary>
public interface IIssueReportBorrowingIntegration
{
    /// <summary>
    /// Call while the BorrowItem is still Issued, before setting ReturnedAt or a return status.
    /// Reload a tracked BorrowItem afterwards; do not overwrite its Damaged/Lost status.
    /// </summary>
    Task<ConfirmBorrowingReturnIssueResult> ConfirmReturnIssueAsync(
        ConfirmBorrowingReturnIssueCommand command,
        CancellationToken ct = default);

    /// <summary>
    /// Reject all Open reports for the exact BorrowItem on a validated Good return.
    /// The caller validates the receiving room and line, updates the ComponentItem from
    /// Borrowed to Available in SQL (holding its row lock), and records Return +1 before
    /// calling this method in the same transaction. The caller owns return metadata,
    /// request status, tracking refresh and commit/rollback. This method only updates reports.
    /// </summary>
    Task<int> RejectOpenReportsOnGoodReturnAsync(Guid borrowItemId, CancellationToken ct = default);
}

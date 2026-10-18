using SmartLab.BLL.DTOs.IssueReports;

namespace SmartLab.BLL.Interfaces.IssueReports;

/// <summary>
/// Internal integration contract for the Borrowing return workflow.
/// The caller must start, commit and roll back a transaction through the same scoped
/// IUnitOfWork used by this service. This service never owns that transaction. Call it
/// while the BorrowItem is still Issued and before setting ReturnedAt or a return status.
/// A caller already tracking that BorrowItem must reload it after this call and must not
/// overwrite the Damaged/Lost status written by the integration.
/// </summary>
public interface IIssueReportBorrowingIntegration
{
    Task<ConfirmBorrowingReturnIssueResult> ConfirmReturnIssueAsync(
        ConfirmBorrowingReturnIssueCommand command,
        CancellationToken ct = default);
}

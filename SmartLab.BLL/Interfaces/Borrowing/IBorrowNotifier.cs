namespace SmartLab.BLL.Interfaces.Borrowing;

/// <summary>
/// Emails the borrower when their borrow request changes. Call after the change is committed. Never throws because of
/// the mail server: failures are only logged, so the business action still succeeds.
/// </summary>
public interface IBorrowNotifier
{
    /// <summary>A new request waits for approval: tells the lecturer of the pending approval step.</summary>
    Task SubmittedAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>Approved by the lecturer: the lab checks the goods, another mail follows when the request is Ready.</summary>
    Task ApprovedAsync(Guid borrowRequestId, CancellationToken ct = default);

    Task RejectedAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>Everything is at the pickup lab: come and pick it up with the student card.</summary>
    Task ReadyAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>Receipt after handover: every component and unit taken, and the due date.</summary>
    Task IssuedAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>
    /// Confirms one return: the lines returned this time with their condition, and what is still out (or that
    /// everything is back).
    /// </summary>
    Task ReturnedAsync(Guid borrowRequestId, IReadOnlyCollection<Guid> returnedLineIds, CancellationToken ct = default);

    /// <summary>
    /// Lab staff recorded a lent unit as broken, burnt or lost without giving another one: what was recorded and what is
    /// still out.
    /// </summary>
    Task IssueRecordedAsync(Guid borrowRequestId, Guid lineId, CancellationToken ct = default);

    /// <summary>
    /// A broken unit was swapped: tells the borrower the replacement is ready at the request's lab, or that the lab has none
    /// left (<paramref name="replacementLineId"/> null).
    /// </summary>
    Task ReplacementAsync(Guid borrowRequestId, Guid damagedLineId, Guid? replacementLineId, CancellationToken ct = default);

    /// <summary>Receipt after handing a replacement unit over.</summary>
    Task ReplacementIssuedAsync(Guid borrowRequestId, Guid replacementLineId, CancellationToken ct = default);

    /// <summary>A replacement was dropped: tells the borrower with the reason.</summary>
    Task ReplacementCancelledAsync(Guid borrowRequestId, string componentName, string reason, CancellationToken ct = default);

    /// <summary>The due date is tomorrow and components are still out.</summary>
    Task DueSoonAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>The due date has passed by <paramref name="daysOverdue"/> days and components are still out.</summary>
    Task OverdueAsync(Guid borrowRequestId, int daysOverdue, CancellationToken ct = default);

    /// <summary>Cancelled by staff; the reason is in RejectReason.</summary>
    Task CancelledByStaffAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>
    /// Tells the request's lab staff about a newly approved request so they check the goods and accept it.
    /// </summary>
    Task StaffApprovedAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>
    /// Tells the request's lab staff (except the one who cancelled) that a request was cancelled.
    /// </summary>
    Task StaffCancelledAsync(Guid borrowRequestId, Guid cancelledBy, CancellationToken ct = default);

    /// <summary>The borrower asked to extend the due date: tells the lecturer of the class.</summary>
    Task ExtensionRequestedAsync(Guid extensionId, CancellationToken ct = default);

    /// <summary>The lecturer approved (new due date) or rejected (reason) an extension: tells the borrower.</summary>
    Task ExtensionReviewedAsync(Guid extensionId, CancellationToken ct = default);
}

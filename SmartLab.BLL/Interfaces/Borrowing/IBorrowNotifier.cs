namespace SmartLab.BLL.Interfaces.Borrowing;

/// <summary>
/// Emails the borrower when their borrow request changes. Call after the change is committed. Never throws because of
/// the mail server: failures are only logged, so the business action still succeeds.
/// </summary>
public interface IBorrowNotifier
{
    /// <summary>A new request waits for approval: tells the lecturer of the pending approval step.</summary>
    Task SubmittedAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>Approved, but components still have to be carried from other labs to the pickup lab.</summary>
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
    /// A broken unit was swapped: tells the borrower the replacement is ready at the pickup lab, is being carried from
    /// another lab, or that no lab has one left (<paramref name="replacementLineId"/> null). When it must be carried over,
    /// also tells the staff of the source lab.
    /// </summary>
    Task ReplacementAsync(Guid borrowRequestId, Guid damagedLineId, Guid? replacementLineId, CancellationToken ct = default);

    /// <summary>A replacement carried from another lab reached the pickup lab: come and pick it up with the student card.</summary>
    Task ReplacementArrivedAsync(Guid borrowRequestId, Guid replacementLineId, CancellationToken ct = default);

    /// <summary>Receipt after handing a replacement unit over.</summary>
    Task ReplacementIssuedAsync(Guid borrowRequestId, Guid replacementLineId, CancellationToken ct = default);

    /// <summary>
    /// A replacement was dropped: tells the borrower with the reason, and the source lab (when the unit was not carried
    /// over yet, <paramref name="notCarriedFromRoomId"/>) that it no longer needs to bring it.
    /// </summary>
    Task ReplacementCancelledAsync(
        Guid borrowRequestId, string componentName, Guid? notCarriedFromRoomId, string reason, Guid cancelledBy,
        CancellationToken ct = default);

    /// <summary>The due date is tomorrow and components are still out.</summary>
    Task DueSoonAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>The due date has passed by <paramref name="daysOverdue"/> days and components are still out.</summary>
    Task OverdueAsync(Guid borrowRequestId, int daysOverdue, CancellationToken ct = default);

    /// <summary>Cancelled by staff; the reason is in RejectReason.</summary>
    Task CancelledByStaffAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>
    /// Tells lab staff about a newly approved request: staff of each source lab get the lines they must carry to the
    /// pickup lab, staff of the pickup lab get the whole request (what is coming from where, or that it is ready).
    /// </summary>
    Task StaffApprovedAsync(Guid borrowRequestId, CancellationToken ct = default);

    /// <summary>
    /// Tells lab staff that a request was cancelled: source labs with lines not yet carried over no longer need to bring
    /// them, and the pickup lab staff (except the one who cancelled) stop preparing it.
    /// </summary>
    Task StaffCancelledAsync(Guid borrowRequestId, Guid cancelledBy, CancellationToken ct = default);

    /// <summary>The borrower asked to extend the due date: tells the lecturer of the class.</summary>
    Task ExtensionRequestedAsync(Guid extensionId, CancellationToken ct = default);

    /// <summary>The lecturer approved (new due date) or rejected (reason) an extension: tells the borrower.</summary>
    Task ExtensionReviewedAsync(Guid extensionId, CancellationToken ct = default);
}

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
}

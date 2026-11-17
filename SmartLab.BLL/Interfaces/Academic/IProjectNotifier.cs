namespace SmartLab.BLL.Interfaces.Academic;

/// <summary>
/// Emails about the review of a project that borrows nothing from the lab (the borrow notifier covers the others) and
/// about its final submission. Call after the change is saved. Never throws because of the mail
/// server: failures are only logged, so the business action still succeeds.
/// </summary>
public interface IProjectNotifier
{
    /// <summary>A team submitted a project that only uses self-supplied items: tells the lecturer of the class.</summary>
    Task SubmittedAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>The lecturer approved or rejected a project that only uses self-supplied items: tells the team leader.</summary>
    Task ReviewedAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>A team submitted its final work: tells the lecturer of the class.</summary>
    Task FinalSubmittedAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>The lecturer accepted or returned the final work: tells the team leader.</summary>
    Task FinalReviewedAsync(Guid projectId, CancellationToken ct = default);
}

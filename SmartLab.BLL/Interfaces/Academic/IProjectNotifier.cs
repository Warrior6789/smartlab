namespace SmartLab.BLL.Interfaces.Academic;

/// <summary>
/// Emails about the final submission of a project. Call after the change is saved. Never throws because of the mail
/// server: failures are only logged, so the business action still succeeds.
/// </summary>
public interface IProjectNotifier
{
    /// <summary>A team submitted its final work: tells the lecturer of the class.</summary>
    Task FinalSubmittedAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>The lecturer accepted or returned the final work: tells the team leader.</summary>
    Task FinalReviewedAsync(Guid projectId, CancellationToken ct = default);
}

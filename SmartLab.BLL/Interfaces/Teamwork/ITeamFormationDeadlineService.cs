namespace SmartLab.BLL.Interfaces.Teamwork;

/// <summary>What happens around the team formation deadline of the classes of active semesters.</summary>
public interface ITeamFormationDeadlineService
{
    /// <summary>
    /// Reminds students without a team in the last day before the deadline. After it, rejects unanswered invitations and
    /// tells the lecturer when the class still has students without a team or teams below the minimum. Safe to repeat: each
    /// person is told once per deadline.
    /// </summary>
    Task RunAsync(CancellationToken ct = default);
}

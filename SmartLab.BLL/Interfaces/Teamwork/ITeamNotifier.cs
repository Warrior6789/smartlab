namespace SmartLab.BLL.Interfaces.Teamwork;

/// <summary>Notifications sent only after the team mutation commits; mail failures do not undo the action.</summary>
public interface ITeamNotifier
{
    /// <summary>The leader invited a student: tells the invited student.</summary>
    Task InvitationSentAsync(Guid teamId, Guid studentProfileId, CancellationToken ct = default);

    /// <summary>A student accepted or rejected an invitation: tells the team leader.</summary>
    Task InvitationAnsweredAsync(Guid teamId, Guid studentProfileId, bool accepted, CancellationToken ct = default);

    Task MemberRemovedAsync(Guid teamId, Guid studentProfileId, CancellationToken ct = default);
    Task LeaderTransferredAsync(Guid teamId, Guid oldLeaderId, Guid newLeaderId, CancellationToken ct = default);
}

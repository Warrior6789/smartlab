namespace SmartLab.BLL.Interfaces.Teamwork;

/// <summary>Notifications sent only after the team mutation commits; mail failures do not undo the action.</summary>
public interface ITeamNotifier
{
    Task MemberRemovedAsync(Guid teamId, Guid studentProfileId, CancellationToken ct = default);
    Task LeaderTransferredAsync(Guid teamId, Guid oldLeaderId, Guid newLeaderId, CancellationToken ct = default);
}

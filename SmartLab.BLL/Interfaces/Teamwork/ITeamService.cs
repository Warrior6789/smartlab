using SmartLab.BLL.DTOs.Teamwork;

namespace SmartLab.BLL.Interfaces.Teamwork;

public interface ITeamService
{
    Task<TeamResponse> CreateTeamAsync(Guid classId, CreateTeamRequest request, CancellationToken ct = default);
    Task<TeamResponse> GetMyTeamAsync(Guid classId, CancellationToken ct = default);
    Task<IReadOnlyList<TeamCandidateResponse>> GetCandidatesAsync(Guid classId, CancellationToken ct = default);
    Task<TeamResponse> GetTeamAsync(Guid teamId, CancellationToken ct = default);
    Task<TeamResponse> InviteMemberAsync(Guid teamId, InviteTeamMemberRequest request, CancellationToken ct = default);
    Task<TeamResponse> RemoveUnacceptedMemberAsync(Guid teamId, Guid studentProfileId, CancellationToken ct = default);
    Task<IReadOnlyList<TeamInvitationResponse>> GetMyInvitationsAsync(CancellationToken ct = default);
    Task<TeamResponse> RespondToInvitationAsync(Guid teamId, RespondTeamInvitationRequest request, CancellationToken ct = default);
    /// <summary>An Accepted member leaves before the team has a project. A leader who is alone dissolves the team; otherwise the leader hands over first.</summary>
    Task<TeamResponse> LeaveTeamAsync(Guid teamId, CancellationToken ct = default);
    Task<TeamResponse> RemoveMemberAsync(Guid teamId, Guid studentProfileId, CancellationToken ct = default);
    /// <summary>
    /// The leader asks a member to take over (the member confirms), only before the team registers a project. The class
    /// lecturer or Admin changes the leader at once at any stage, e.g. when the leader drops out.
    /// </summary>
    Task<TeamResponse> TransferLeaderAsync(Guid teamId, TransferTeamLeaderRequest request, CancellationToken ct = default);

    /// <summary>The member asked to become the leader accepts (becomes the leader) or rejects.</summary>
    Task<TeamResponse> RespondLeaderTransferAsync(Guid teamId, RespondLeaderTransferRequest request, CancellationToken ct = default);
}

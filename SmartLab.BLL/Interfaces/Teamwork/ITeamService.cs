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
}

using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Interfaces.Academic;

public interface ITeamService
{
    Task<TeamResponse> CreateTeamAsync(CreateTeamRequest request, CancellationToken ct = default);
    Task<List<AvailableStudentResponse>> GetAvailableStudentsAsync(Guid classId, string? search = null, CancellationToken ct = default);
    Task<List<TeamResponse>> GetTeamsByClassAsync(Guid classId, CancellationToken ct = default);
    Task<TeamResponse> GetTeamByIdAsync(Guid teamId, CancellationToken ct = default);
    Task<TeamResponse> InviteMemberAsync(Guid teamId, InviteMemberRequest request, CancellationToken ct = default);
    Task<List<MyInvitationResponse>> GetMyInvitationsAsync(CancellationToken ct = default);
    Task RespondInvitationAsync(Guid teamId, RespondInvitationRequest request, CancellationToken ct = default);
}
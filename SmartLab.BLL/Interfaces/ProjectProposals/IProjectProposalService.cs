using SmartLab.BLL.DTOs.ProjectProposals;

namespace SmartLab.BLL.Interfaces.ProjectProposals;

public interface IProjectProposalService
{
    Task<ProjectProposalResponse> SubmitAsync(
        Guid teamId, SubmitProjectProposalRequest request, CancellationToken ct = default);

    Task<ProjectProposalResponse> GetByTeamAsync(Guid teamId, CancellationToken ct = default);

    Task<ProjectProposalResponse> ResubmitAsync(
        Guid projectId, SubmitProjectProposalRequest request, CancellationToken ct = default);
}

using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Interfaces.Academic;

public interface IProjectService
{
    Task<ProjectResponse> SubmitProjectAsync(SubmitProjectRequest request, CancellationToken ct = default);
    Task<ProjectResponse> ReviewProjectAsync(Guid projectId, ReviewProjectRequest request, CancellationToken ct = default);
    Task<List<ProjectResponse>> GetProjectsByClassAsync(Guid classId, CancellationToken ct = default);
    Task<ProjectResponse> GetProjectByIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectResponse?> GetProjectByTeamAsync(Guid teamId, CancellationToken ct = default);
}
using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.ProjectProposals;

namespace SmartLab.BLL.Interfaces.ProjectProposals;

public interface IProjectProposalReviewService
{
    Task<PagedResult<ProjectProposalReviewItemResponse>> GetAssignedAsync(
        PageParams paging, CancellationToken ct = default);

    Task<ProjectProposalReviewDetailResponse> GetDetailAsync(
        Guid projectId, CancellationToken ct = default);

    Task<ProjectProposalReviewDetailResponse> ReviewAsync(
        Guid projectId, ReviewProjectProposalRequest request, CancellationToken ct = default);
}

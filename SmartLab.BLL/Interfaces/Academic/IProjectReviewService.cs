using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Interfaces.Academic;

/// <summary>
/// Review of submitted projects. The approval steps live on the borrow request created when the project was submitted
/// (lecturer, then department head or Admin for HIGH_VALUE components).
/// </summary>
public interface IProjectReviewService
{
    /// <summary>
    /// Submitted projects whose current step (lowest step still Pending, all earlier steps Approved) is assigned to the
    /// caller; Admin sees every current step. Oldest submission first.
    /// </summary>
    Task<PagedResult<PendingApprovalProjectResponse>> GetPendingAsync(PendingApprovalQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>Approves the current step (its approver or an Admin standing in). The last step approves the project and its borrow request.</summary>
    Task<ProjectDetailResponse> ApproveAsync(Guid projectId, ApproveProjectRequest request, CancellationToken ct = default);

    /// <summary>Rejects the current step: the project and its borrow request become Rejected; the team can edit and send again.</summary>
    Task<ProjectDetailResponse> RejectAsync(Guid projectId, RejectProjectRequest request, CancellationToken ct = default);
}

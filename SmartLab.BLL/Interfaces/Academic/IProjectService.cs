using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Interfaces.Academic;

public interface IProjectService
{
    /// <summary>
    /// Paged projects the current user can see, newest first: Admin sees all; others see the projects of their own team,
    /// of the classes they teach, of the classes taught in the department they head, and of the borrow requests they approve.
    /// </summary>
    Task<PagedResult<ProjectResponse>> GetProjectsAsync(ProjectQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>Project detail with its components and borrow requests; same visibility as the list.</summary>
    Task<ProjectDetailResponse> GetProjectByIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Team leader saves the team's project as Draft; a team has at most one project.</summary>
    Task<ProjectDetailResponse> CreateProjectAsync(Guid teamId, CreateProjectRequest request, CancellationToken ct = default);

    /// <summary>Team leader replaces title, description, components and self-supplied items while the project is Draft or Rejected.</summary>
    Task<ProjectDetailResponse> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct = default);

    /// <summary>Team leader sets the GitHub / slide / demo video links in any status.</summary>
    Task<ProjectDetailResponse> UpdateProjectLinksAsync(Guid projectId, UpdateProjectLinksRequest request, CancellationToken ct = default);

    /// <summary>
    /// Team leader sends a Draft or Rejected project for review: the project becomes Submitted and a Pending borrow request
    /// for its components is created in the chosen lab, with approval steps lecturer (+ department head, or Admin standing in,
    /// when it has HIGH_VALUE components).
    /// </summary>
    Task<ProjectDetailResponse> SubmitProjectAsync(Guid projectId, SubmitProjectRequest request, CancellationToken ct = default);

    /// <summary>
    /// Team leader takes back a Submitted project that is not decided yet: its Pending borrow request becomes Cancelled
    /// and the project goes back to Draft.
    /// </summary>
    Task<ProjectDetailResponse> WithdrawProjectAsync(Guid projectId, CancellationToken ct = default);
}

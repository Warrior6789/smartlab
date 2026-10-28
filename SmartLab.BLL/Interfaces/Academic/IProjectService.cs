using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Interfaces.Academic;

public interface IProjectService
{
    /// <summary>
    /// Paged projects the current user can see, newest first: Admin sees all; others see the projects of their own team,
    /// of the classes they teach, and of the borrow requests they approve.
    /// </summary>
    Task<PagedResult<ProjectResponse>> GetProjectsAsync(ProjectQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>Project detail with its components and borrow requests; same visibility as the list.</summary>
    Task<ProjectDetailResponse> GetProjectByIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Team leader saves the team's project as Draft; a team has at most one project.</summary>
    Task<ProjectDetailResponse> CreateProjectAsync(Guid teamId, CreateProjectRequest request, CancellationToken ct = default);

    /// <summary>Team leader replaces title, description, components and self-supplied items while the project is Draft or Rejected.</summary>
    Task<ProjectDetailResponse> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct = default);

    /// <summary>Team leader sets the GitHub / slide / demo video links, except while the final submission is Pending or Accepted.</summary>
    Task<ProjectDetailResponse> UpdateProjectLinksAsync(Guid projectId, UpdateProjectLinksRequest request, CancellationToken ct = default);

    /// <summary>Team leader uploads the final report PDF (replaces the old one), except while the final submission is Pending or Accepted.</summary>
    Task<ProjectDetailResponse> UploadReportAsync(Guid projectId, UploadProjectReportRequest request, CancellationToken ct = default);

    /// <summary>Team leader removes the final report PDF, except while the final submission is Pending or Accepted.</summary>
    Task<ProjectDetailResponse> DeleteReportAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Team leader uploads the source code ZIP (replaces the old one), except while the final submission is Pending or Accepted.</summary>
    Task<ProjectDetailResponse> UploadSourceCodeAsync(Guid projectId, UploadProjectSourceCodeRequest request, CancellationToken ct = default);

    /// <summary>Team leader removes the source code ZIP, except while the final submission is Pending or Accepted.</summary>
    Task<ProjectDetailResponse> DeleteSourceCodeAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Team leader of an Approved project asks to borrow more components (with a reason). Creates a Pending borrow request
    /// for the class lecturer, decided through the same approve / reject as the project; the project stays Approved.
    /// One pending request at a time; not allowed after the final submission.
    /// </summary>
    Task<ProjectDetailResponse> SubmitSupplementaryBorrowAsync(Guid projectId, SupplementaryBorrowRequest request, CancellationToken ct = default);

    /// <summary>Team leader withdraws the supplementary borrow request still waiting for the lecturer.</summary>
    Task<ProjectDetailResponse> WithdrawSupplementaryBorrowAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Team leader submits the final work (Approved project, all three links set, report PDF uploaded); it waits for the class lecturer.</summary>
    Task<ProjectDetailResponse> SubmitFinalAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Pending final submissions of the classes the caller teaches (Admin: all), oldest submission first.</summary>
    Task<PagedResult<PendingFinalSubmissionResponse>> GetPendingFinalSubmissionsAsync(
        FinalSubmissionQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>The class lecturer (or Admin) accepts a Pending final submission; the links stay locked.</summary>
    Task<ProjectDetailResponse> ApproveFinalAsync(Guid projectId, ApproveFinalSubmissionRequest request, CancellationToken ct = default);

    /// <summary>The class lecturer (or Admin) returns a Pending final submission with a comment; the team can fix and submit again.</summary>
    Task<ProjectDetailResponse> RejectFinalAsync(Guid projectId, RejectFinalSubmissionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Team leader sends a Draft or Rejected project for review: the project becomes Submitted and a Pending borrow request
    /// for its components is created in the chosen lab, with one approval step for the class lecturer (also when it has
    /// HIGH_VALUE components).
    /// </summary>
    Task<ProjectDetailResponse> SubmitProjectAsync(Guid projectId, SubmitProjectRequest request, CancellationToken ct = default);

    /// <summary>
    /// Team leader takes back a Submitted project that is not decided yet: its Pending borrow request becomes Cancelled
    /// and the project goes back to Draft.
    /// </summary>
    Task<ProjectDetailResponse> WithdrawProjectAsync(Guid projectId, CancellationToken ct = default);
}

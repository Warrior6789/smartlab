using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.IssueReports;

namespace SmartLab.BLL.Interfaces.IssueReports;

public interface IIssueReportService
{
    Task<IssueReportDetailResponse> CreateAsync(
        CreateIssueReportRequest request,
        CancellationToken ct = default);

    Task<IssueReportDetailResponse> ResolveAsync(
        Guid issueReportId,
        ResolveIssueReportRequest request,
        CancellationToken ct = default);

    Task<IssueReportDetailResponse> CancelAsync(
        Guid issueReportId,
        CancellationToken ct = default);

    Task<PagedResult<IssueReportListItemResponse>> GetAsync(
        IssueReportQueryParams query,
        PageParams paging,
        CancellationToken ct = default);

    Task<IssueReportDetailResponse> GetByIdAsync(
        Guid issueReportId,
        CancellationToken ct = default);
}
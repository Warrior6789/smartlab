using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.ProjectProposals;

namespace SmartLab.BLL.Interfaces.ProjectProposals;

public interface IProjectComponentCatalogService
{
    Task<PagedResult<ProjectComponentOptionResponse>> GetOptionsAsync(
        string? search, PageParams paging, CancellationToken ct = default);
}

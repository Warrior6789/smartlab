using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ProjectProposals;
using SmartLab.BLL.Interfaces.ProjectProposals;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.ProjectProposals;

public class ProjectComponentCatalogService : IProjectComponentCatalogService
{
    private readonly IUnitOfWork _uow;

    public ProjectComponentCatalogService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public Task<PagedResult<ProjectComponentOptionResponse>> GetOptionsAsync(
        string? search, PageParams paging, CancellationToken ct = default)
    {
        var components = _uow.Repository<Component>().QueryNoTracking()
            .Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            components = components.Where(c =>
                c.ComponentCode.ToLower().Contains(keyword) ||
                c.ComponentName.ToLower().Contains(keyword));
        }

        var result = components
            .OrderBy(c => c.ComponentCode)
            .Select(c => new ProjectComponentOptionResponse
            {
                ComponentId = c.ComponentId,
                ComponentCode = c.ComponentCode,
                ComponentName = c.ComponentName,
                CategoryName = c.Category.CategoryName,
                Manufacturer = c.Manufacturer,
                AvailableQuantity = c.ComponentItems.Count(ci => ci.Status == ComponentItemStatuses.Available),
            });

        return PagedResult<ProjectComponentOptionResponse>.CreateAsync(result, paging, ct);
    }
}

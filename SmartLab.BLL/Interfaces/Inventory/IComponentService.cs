using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface IComponentService
{
    /// <summary>Paged component catalog with total and available quantities.</summary>
    Task<PagedResult<ComponentResponse>> GetComponentsAsync(ComponentQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>Component detail with specs and stock per cabinet.</summary>
    Task<ComponentDetailResponse> GetComponentByIdAsync(Guid componentId, CancellationToken ct = default);

    /// <summary>Creates a catalog item with its specs in one save; the code is generated as LK-&lt;nnnn&gt;.</summary>
    Task<ComponentDetailResponse> CreateComponentAsync(CreateComponentRequest request, CancellationToken ct = default);

    /// <summary>Updates info and replaces the spec list (component code cannot change).</summary>
    Task<ComponentDetailResponse> UpdateComponentAsync(Guid componentId, UpdateComponentRequest request, CancellationToken ct = default);

    /// <summary>Uploads a PDF datasheet and stores its URL; the previous uploaded PDF (if any) is deleted.</summary>
    Task<ComponentDetailResponse> UploadDatasheetAsync(Guid componentId, UploadDatasheetRequest request, CancellationToken ct = default);

    /// <summary>Removes the datasheet (link or uploaded PDF); an uploaded PDF is deleted from storage.</summary>
    Task DeleteDatasheetAsync(Guid componentId, CancellationToken ct = default);
}

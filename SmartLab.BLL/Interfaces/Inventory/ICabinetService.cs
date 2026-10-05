using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface ICabinetService
{
    /// <summary>
    /// Paged storage cabinets ordered by room code then cabinet code, filtered by search text, room and status.
    /// ItemCount excludes Lost/Retired units.
    /// </summary>
    Task<PagedResult<CabinetResponse>> GetCabinetsAsync(CabinetQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>Cabinet detail with what it holds, one row per component (Lost/Retired units not counted).</summary>
    Task<CabinetDetailResponse> GetCabinetByIdAsync(Guid cabinetId, CancellationToken ct = default);

    /// <summary>Creates a cabinet with status Active in an Active room; the code is generated as &lt;room code&gt;-T&lt;nn&gt;.</summary>
    Task<CabinetResponse> CreateCabinetAsync(CreateCabinetRequest request, CancellationToken ct = default);

    /// <summary>Updates name, room, location and status (the code never changes); cannot set Inactive while it still holds items that are not Lost/Retired.</summary>
    Task<CabinetResponse> UpdateCabinetAsync(Guid cabinetId, UpdateCabinetRequest request, CancellationToken ct = default);
}

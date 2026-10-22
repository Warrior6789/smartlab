using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface IComponentItemService
{
    /// <summary>Paged physical units, filtered by component, cabinet, status and serial search.</summary>
    Task<PagedResult<ComponentItemResponse>> GetItemsAsync(ComponentItemQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>One physical unit.</summary>
    Task<ComponentItemResponse> GetItemByIdAsync(Guid itemId, CancellationToken ct = default);

    /// <summary>
    /// Corrects serial, version, warranty, condition and note of one unit.
    /// HIGH_VALUE units must keep a serial that is unique per component; other units cannot have serial/version.
    /// </summary>
    Task<ComponentItemResponse> UpdateItemAsync(Guid itemId, UpdateComponentItemRequest request, CancellationToken ct = default);

    /// <summary>
    /// Picks the requested quantity of matching units (oldest first) and moves them to the target cabinet, or brings Lost /
    /// Retired units back as Available. Borrowed units are never touched and the target cabinet must be Active. Taking
    /// units out of stock uses RetireItemAsync. Available units reserved by Approved requests cannot go to another lab (409).
    /// </summary>
    Task<MoveComponentItemsResponse> MoveItemsAsync(MoveComponentItemsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Marks one specific unit as Lost or Retired and records it in inventory_transactions with the reason.
    /// Borrowed units must be returned first. Retiring a reserved Available unit is refused (409); marking it Lost is
    /// allowed and returns the resulting shortages.
    /// </summary>
    Task<RetireComponentItemResponse> RetireItemAsync(Guid itemId, RetireComponentItemRequest request, CancellationToken ct = default);
}

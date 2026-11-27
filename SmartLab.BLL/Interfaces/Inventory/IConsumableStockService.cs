using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface IConsumableStockService
{
    /// <summary>Paged CONSUMABLE stock per component and cabinet, filtered by search text, component, cabinet and room.</summary>
    Task<PagedResult<ConsumableStockResponse>> GetStocksAsync(ConsumableStockQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>Moves CONSUMABLE stock between two cabinets. Returns the source and target stock after the move.</summary>
    Task<IReadOnlyList<ConsumableStockResponse>> MoveStockAsync(MoveConsumableStockRequest request, CancellationToken ct = default);
}

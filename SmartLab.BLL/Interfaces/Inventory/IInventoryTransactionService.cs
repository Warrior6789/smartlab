using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface IInventoryTransactionService
{
    Task<PagedResult<InventoryTransactionResponse>> GetTransactionsAsync(InventoryTransactionQueryParams query, PageParams paging, CancellationToken ct = default);
}

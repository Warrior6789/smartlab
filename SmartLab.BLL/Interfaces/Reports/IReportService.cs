using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Reports;

namespace SmartLab.BLL.Interfaces.Reports;

public interface IReportService
{
    Task<PagedResult<StockSummaryResponse>> GetStockSummaryAsync(
        StockSummaryQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<PagedResult<LowStockResponse>> GetLowStockAsync(
        StockSummaryQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<StockMovementResponse> GetStockMovementsAsync(StockMovementQueryParams query, CancellationToken ct = default);

    Task<PagedResult<OverdueBorrowResponse>> GetOverdueBorrowsAsync(
        OverdueBorrowQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<IReadOnlyList<TopComponentResponse>> GetTopComponentsAsync(TopComponentQueryParams query, CancellationToken ct = default);

    Task<DashboardResponse> GetDashboardAsync(CancellationToken ct = default);
}

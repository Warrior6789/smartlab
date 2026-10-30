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

    Task<StockValueResponse> GetStockValueAsync(CancellationToken ct = default);

    Task<ReceiptReportResponse> GetReceiptReportAsync(ReceiptReportQueryParams query, CancellationToken ct = default);

    Task<LossReportResponse> GetLossesAsync(ReportPeriodQueryParams query, CancellationToken ct = default);

    Task<PagedResult<WarrantyExpiringResponse>> GetWarrantyExpiringAsync(
        WarrantyQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<BorrowSummaryResponse> GetBorrowSummaryAsync(BorrowReportQueryParams query, CancellationToken ct = default);

    Task<IReadOnlyList<ClassUsageResponse>> GetClassUsageAsync(BorrowReportQueryParams query, CancellationToken ct = default);

    Task<PagedResult<DamageResponse>> GetDamagesAsync(
        BorrowReportQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<IssueSummaryResponse> GetIssueSummaryAsync(ReportPeriodQueryParams query, CancellationToken ct = default);

    Task<PagedResult<OverdueBorrowResponse>> GetOverdueBorrowsAsync(
        OverdueBorrowQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<IReadOnlyList<TopComponentResponse>> GetTopComponentsAsync(TopComponentQueryParams query, CancellationToken ct = default);

    Task<DashboardResponse> GetDashboardAsync(CancellationToken ct = default);
}

using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Interfaces.Borrowing;

public interface IBorrowExtensionService
{
    Task<BorrowExtensionResponse> RequestAsync(
        Guid borrowRequestId, RequestExtensionRequest request, CancellationToken ct = default);

    Task<PagedResult<BorrowExtensionResponse>> GetExtensionsAsync(
        BorrowExtensionQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<BorrowExtensionResponse> ApproveAsync(
        Guid extensionId, ApproveExtensionRequest request, CancellationToken ct = default);

    Task<BorrowExtensionResponse> RejectAsync(
        Guid extensionId, RejectExtensionRequest request, CancellationToken ct = default);
}

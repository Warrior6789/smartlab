using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Interfaces.Borrowing;

public interface IBorrowRequestService
{
    Task<PagedResult<BorrowRequestResponse>> GetMyRequestsAsync(
        BorrowRequestQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<PagedResult<BorrowRequestResponse>> GetRequestsAsync(
        BorrowRequestQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<BorrowRequestDetailResponse> GetRequestByIdAsync(Guid borrowRequestId, CancellationToken ct = default);

    Task<PagedResult<TransferLineResponse>> GetTransfersAsync(
        TransferQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<BorrowRequestDetailResponse> ReceiveTransferAsync(
        Guid borrowRequestId, ReceiveTransferRequest request, CancellationToken ct = default);

    Task<HandoverPhotoResponse> UploadPhotoAsync(
        Guid borrowRequestId, UploadHandoverPhotoRequest request, CancellationToken ct = default);

    Task<BorrowRequestDetailResponse> IssueAsync(
        Guid borrowRequestId, IssueBorrowRequest request, CancellationToken ct = default);

    Task<BorrowRequestDetailResponse> ReturnAsync(
        Guid borrowRequestId, ReturnBorrowRequest request, CancellationToken ct = default);

    Task<BorrowRequestDetailResponse> CancelAsync(
        Guid borrowRequestId, CancelBorrowRequest request, CancellationToken ct = default);
}

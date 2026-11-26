using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Interfaces.Borrowing;

/// <summary>
/// Return slips: the borrower asks to bring components back, the lab's staff approve and pick the day, and the goods are received
/// through <see cref="IBorrowRequestService.ReturnAsync"/> with the slip id. A slip never changes the stock by itself.
/// </summary>
public interface IReturnSlipService
{
    Task<ReturnSlipDetailResponse> CreateAsync(Guid borrowRequestId, CreateReturnSlipRequest request, CancellationToken ct = default);

    /// <summary>Slips of my own requests and my team's, newest first.</summary>
    Task<PagedResult<ReturnSlipResponse>> GetMySlipsAsync(ReturnSlipQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>Staff: slips of the labs they are assigned to (Admin all), newest first.</summary>
    Task<PagedResult<ReturnSlipResponse>> GetSlipsAsync(ReturnSlipQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<ReturnSlipDetailResponse> GetByIdAsync(Guid returnSlipId, CancellationToken ct = default);

    Task<ReturnSlipDetailResponse> ApproveAsync(Guid returnSlipId, ApproveReturnSlipRequest request, CancellationToken ct = default);

    /// <summary>The lab moves an approved slip to another day.</summary>
    Task<ReturnSlipDetailResponse> RescheduleAsync(Guid returnSlipId, RescheduleReturnSlipRequest request, CancellationToken ct = default);

    Task<ReturnSlipDetailResponse> RejectAsync(Guid returnSlipId, RejectReturnSlipRequest request, CancellationToken ct = default);

    /// <summary>The borrower drops a slip that was not received yet.</summary>
    Task<ReturnSlipDetailResponse> CancelAsync(Guid returnSlipId, CancellationToken ct = default);
}

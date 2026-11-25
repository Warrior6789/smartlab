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

    Task<StudentCardReadResponse> ReadStudentCardAsync(ReadStudentCardRequest request, CancellationToken ct = default);

    Task<HandoverPhotoResponse> UploadPhotoAsync(
        Guid borrowRequestId, UploadHandoverPhotoRequest request, CancellationToken ct = default);

    Task<BorrowRequestDetailResponse> IssueAsync(
        Guid borrowRequestId, IssueBorrowRequest request, CancellationToken ct = default);

    Task<BorrowRequestDetailResponse> ReturnAsync(
        Guid borrowRequestId, ReturnBorrowRequest request, CancellationToken ct = default);

    Task<ReplaceDamagedResponse> ReplaceDamagedAsync(
        Guid borrowRequestId, ReplaceDamagedRequest request, CancellationToken ct = default);

    /// <summary>
    /// Closes the line of a lent unit that lab staff found broken, burnt or lost, with a Confirmed issue report, and adds
    /// a replacement line when asked for and the lab has a free unit.
    /// </summary>
    Task<RecordLoanIssueResult> RecordLoanIssueAsync(RecordLoanIssueCommand command, CancellationToken ct = default);

    Task<BorrowRequestDetailResponse> IssueReplacementAsync(
        Guid borrowRequestId, IssueReplacementRequest request, CancellationToken ct = default);

    Task<BorrowRequestDetailResponse> CancelReplacementAsync(
        Guid borrowRequestId, CancelReplacementRequest request, CancellationToken ct = default);

    /// <summary>
    /// The pickup lab checked the goods and picks the day to come, from today to the due date (today when none is sent):
    /// Approved becomes Ready and the student is told.
    /// </summary>
    Task<BorrowRequestDetailResponse> LabAcceptAsync(
        Guid borrowRequestId, LabAcceptRequest request, CancellationToken ct = default);

    /// <summary>The lab moves the pickup day of a Ready request; the student is told.</summary>
    Task<BorrowRequestDetailResponse> ChangePickupDateAsync(
        Guid borrowRequestId, ChangePickupDateRequest request, CancellationToken ct = default);

    Task<BorrowRequestDetailResponse> CancelAsync(
        Guid borrowRequestId, CancelBorrowRequest request, CancellationToken ct = default);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;
using SmartLab.BLL.Interfaces.Borrowing;

namespace SmartLab.API.Controllers.Borrowing;

[ApiController]
[Route("api/borrow-requests")]
[Produces("application/json")]
public class BorrowRequestsController : ControllerBase
{
    private readonly IBorrowRequestService _borrowRequestService;

    public BorrowRequestsController(IBorrowRequestService borrowRequestService)
    {
        _borrowRequestService = borrowRequestService;
    }

    /// <summary>Đơn mượn của tôi: đơn mình gửi và đơn của nhóm mình. Mới nhất lên trước.</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BorrowRequestResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyRequests(
        [FromQuery] BorrowRequestQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _borrowRequestService.GetMyRequestsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<BorrowRequestResponse>>.Ok(result));
    }

    /// <summary>
    /// Danh sách đơn cho nhân viên lab: đơn có lab nhận đồ hoặc lab chuyển hàng là lab mình phụ trách (Admin thấy tất cả).
    /// Lọc theo trạng thái, quá hạn (overdue=true), tìm theo mã đơn, tên project, tên người mượn.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.BorrowIssue)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BorrowRequestResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRequests(
        [FromQuery] BorrowRequestQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _borrowRequestService.GetRequestsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<BorrowRequestResponse>>.Ok(result));
    }

    /// <summary>
    /// Các dòng linh kiện đang chờ mang từ lab khác về lab nhận đồ (đơn Approved). Nhân viên thấy dòng có lab chuyển hàng
    /// hoặc lab nhận đồ là lab mình phụ trách.
    /// </summary>
    [HttpGet("transfers")]
    [Authorize(Policy = PermissionCodes.BorrowIssue)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TransferLineResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTransfers(
        [FromQuery] TransferQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _borrowRequestService.GetTransfersAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<TransferLineResponse>>.Ok(result));
    }

    /// <summary>
    /// Chi tiết đơn: các dòng linh kiện (lab chuyển hàng, đã nhận chưa, con nào đã giao), ảnh giao / trả, lịch sử duyệt.
    /// Người mượn, nhóm, giảng viên của lớp, nhân viên lab liên quan và Admin xem được.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BorrowRequestDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequest(Guid id, CancellationToken ct)
    {
        var request = await _borrowRequestService.GetRequestByIdAsync(id, ct);
        return Ok(ApiResponse<BorrowRequestDetailResponse>.Ok(request));
    }

    /// <summary>
    /// Nhân viên lab nhận đồ xác nhận đã nhận 1 dòng mang từ lab khác về: hàng theo từng con thì quét đúng các con nhận được,
    /// vật tư tiêu hao thì nhận đủ số lượng. Hàng chuyển vào tủ đã chọn. Nhận đủ mọi dòng thì đơn thành Ready.
    /// </summary>
    [HttpPost("{id:guid}/receive")]
    [Authorize(Policy = PermissionCodes.BorrowIssue)]
    [ProducesResponseType(typeof(ApiResponse<BorrowRequestDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReceiveTransfer(Guid id, [FromBody] ReceiveTransferRequest request, CancellationToken ct)
    {
        var result = await _borrowRequestService.ReceiveTransferAsync(id, request, ct);
        return Ok(ApiResponse<BorrowRequestDetailResponse>.Ok(result, "Đã nhận hàng chuyển về"));
    }

    /// <summary>
    /// Tải ảnh giao đồ (photoType = Issue, đơn Ready) hoặc ảnh nhận trả (Return, đơn đang mượn): ảnh chụp linh kiện kèm thẻ
    /// sinh viên (JPG/PNG/WEBP, tối đa 5 MB). Ảnh giao đồ được AI đọc MSSV trên thẻ và so với người mượn
    /// (Match / Mismatch / Unreadable). AI lỗi thì trả Unreadable, không chặn việc giao.
    /// </summary>
    [HttpPost("{id:guid}/photos")]
    [Authorize(Policy = PermissionCodes.BorrowIssue)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<HandoverPhotoResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadPhoto(Guid id, [FromForm] UploadHandoverPhotoRequest request, CancellationToken ct)
    {
        var photo = await _borrowRequestService.UploadPhotoAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<HandoverPhotoResponse>.Ok(photo, "Tải ảnh thành công"));
    }

    /// <summary>
    /// Giao đồ cho sinh viên (Ready → Borrowing). Cần ảnh giao đồ của đơn; AI không xác nhận được thẻ thì phải ghi lý do.
    /// Hàng theo từng con: chọn đúng các con đem giao (đang ở lab nhận đồ). Vật tư tiêu hao tự lấy từ tủ nhiều hàng nhất.
    /// </summary>
    [HttpPost("{id:guid}/issue")]
    [Authorize(Policy = PermissionCodes.BorrowIssue)]
    [ProducesResponseType(typeof(ApiResponse<BorrowRequestDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Issue(Guid id, [FromBody] IssueBorrowRequest request, CancellationToken ct)
    {
        var result = await _borrowRequestService.IssueAsync(id, request, ct);
        return Ok(ApiResponse<BorrowRequestDetailResponse>.Ok(result, "Giao đồ thành công"));
    }

    /// <summary>
    /// Nhận trả tại lab nhận đồ (trả được nhiều lần), cần ảnh nhận trả. Hàng theo từng con: Good / Damaged / Lost.
    /// Vật tư tiêu hao: số lượng mang trả (phần còn lại coi như đã dùng hết) cất vào tủ đã chọn. Trả hết thì đơn thành Returned.
    /// </summary>
    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = PermissionCodes.BorrowIssue)]
    [ProducesResponseType(typeof(ApiResponse<BorrowRequestDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnBorrowRequest request, CancellationToken ct)
    {
        var result = await _borrowRequestService.ReturnAsync(id, request, ct);
        return Ok(ApiResponse<BorrowRequestDetailResponse>.Ok(result, "Nhận trả thành công"));
    }

    /// <summary>
    /// Hủy đơn đã duyệt mà chưa giao đồ (Approved / Ready): người mượn, nhân viên lab nhận đồ hoặc Admin.
    /// Nhân viên / Admin hủy phải ghi lý do (ví dụ lab kho phát hiện hàng hỏng, mất sau khi duyệt); lý do lưu vào rejectReason
    /// và được gửi mail cho sinh viên. Hàng hết được giữ; hàng đã mang về lab nhận đồ thì để luôn ở đó.
    /// Project về Draft để nhóm sửa và gửi duyệt lại.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<BorrowRequestDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelBorrowRequest? request, CancellationToken ct)
    {
        var result = await _borrowRequestService.CancelAsync(id, request ?? new CancelBorrowRequest(), ct);
        return Ok(ApiResponse<BorrowRequestDetailResponse>.Ok(result, "Hủy đơn mượn thành công"));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;
using SmartLab.BLL.Interfaces.Borrowing;

namespace SmartLab.API.Controllers.Borrowing;

[ApiController]
[Route("api/return-slips")]
[Produces("application/json")]
public class ReturnSlipsController : ControllerBase
{
    private readonly IReturnSlipService _returnSlipService;

    public ReturnSlipsController(IReturnSlipService returnSlipService)
    {
        _returnSlipService = returnSlipService;
    }

    /// <summary>
    /// Người gửi đơn mượn (hoặc trưởng nhóm) tạo phiếu trả cho đơn đang mượn: chọn hết hoặc một số món đang mượn. Vật tư tiêu hao
    /// phải ghi số lượng mang trả (0 nếu đã dùng hết). Ngày mang lên do lab chọn khi duyệt. Mỗi món chỉ nằm trong 1 phiếu đang mở.
    /// Tồn kho chưa thay đổi.
    /// </summary>
    [HttpPost("~/api/borrow-requests/{id:guid}/return-slips")]
    [Authorize(Policy = PermissionCodes.BorrowCreate)]
    [ProducesResponseType(typeof(ApiResponse<ReturnSlipDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Guid id, [FromBody] CreateReturnSlipRequest request, CancellationToken ct)
    {
        var result = await _returnSlipService.CreateAsync(id, request, ct);
        return CreatedAtAction(nameof(GetSlip), new { id = result.ReturnSlipId },
            ApiResponse<ReturnSlipDetailResponse>.Ok(result, "Đã gửi phiếu trả, chờ lab duyệt"));
    }

    /// <summary>Phiếu trả của tôi: đơn mình gửi và đơn của nhóm mình. Mới nhất lên trước.</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReturnSlipResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySlips(
        [FromQuery] ReturnSlipQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _returnSlipService.GetMySlipsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<ReturnSlipResponse>>.Ok(result));
    }

    /// <summary>
    /// Danh sách phiếu trả cho nhân viên lab: đơn thuộc lab mình phụ trách (Admin thấy tất cả). Mới nhất lên trước.
    /// Lọc theo trạng thái, đơn mượn, lab, ngày hẹn; tìm theo mã phiếu, mã đơn, tên hoặc MSSV người mượn, tên project.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.BorrowIssue)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReturnSlipResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSlips(
        [FromQuery] ReturnSlipQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _returnSlipService.GetSlipsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<ReturnSlipResponse>>.Ok(result));
    }

    /// <summary>Chi tiết phiếu trả. Người mượn, nhóm, giảng viên của lớp, nhân viên lab của đơn và Admin xem được.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReturnSlipDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSlip(Guid id, CancellationToken ct)
    {
        var result = await _returnSlipService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<ReturnSlipDetailResponse>.Ok(result));
    }

    /// <summary>
    /// Nhân viên lab của đơn (hoặc Admin) duyệt phiếu chờ duyệt và chọn ngày hẹn (từ hôm nay tới hạn trả; đơn quá hạn thì từ hôm
    /// nay); ghi chú không bắt buộc. Tới ngày hẹn, nhận đồ bằng
    /// POST /api/borrow-requests/{id}/return kèm returnSlipId: lúc đó tồn kho mới thay đổi và phiếu thành Completed.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = PermissionCodes.BorrowIssue)]
    [ProducesResponseType(typeof(ApiResponse<ReturnSlipDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveReturnSlipRequest request, CancellationToken ct)
    {
        var result = await _returnSlipService.ApproveAsync(id, request, ct);
        return Ok(ApiResponse<ReturnSlipDetailResponse>.Ok(result, "Đã duyệt phiếu trả"));
    }

    /// <summary>Nhân viên lab của đơn (hoặc Admin) đổi ngày hẹn của phiếu đã duyệt; sinh viên được báo ngày mới.</summary>
    [HttpPost("{id:guid}/reschedule")]
    [Authorize(Policy = PermissionCodes.BorrowIssue)]
    [ProducesResponseType(typeof(ApiResponse<ReturnSlipDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleReturnSlipRequest request, CancellationToken ct)
    {
        var result = await _returnSlipService.RescheduleAsync(id, request, ct);
        return Ok(ApiResponse<ReturnSlipDetailResponse>.Ok(result, "Đã đổi ngày hẹn trả"));
    }

    /// <summary>Nhân viên lab của đơn (hoặc Admin) từ chối phiếu chờ duyệt, bắt buộc ghi lý do; lý do được gửi cho sinh viên.</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = PermissionCodes.BorrowIssue)]
    [ProducesResponseType(typeof(ApiResponse<ReturnSlipDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReturnSlipRequest request, CancellationToken ct)
    {
        var result = await _returnSlipService.RejectAsync(id, request, ct);
        return Ok(ApiResponse<ReturnSlipDetailResponse>.Ok(result, "Đã từ chối phiếu trả"));
    }

    /// <summary>Người gửi đơn mượn (hoặc trưởng nhóm) hủy phiếu đang chờ duyệt hoặc đã duyệt mà chưa mang đồ lên.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionCodes.BorrowCreate)]
    [ProducesResponseType(typeof(ApiResponse<ReturnSlipDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _returnSlipService.CancelAsync(id, ct);
        return Ok(ApiResponse<ReturnSlipDetailResponse>.Ok(result, "Đã hủy phiếu trả"));
    }
}

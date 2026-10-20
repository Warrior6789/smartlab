using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;
using SmartLab.BLL.Interfaces.Borrowing;

namespace SmartLab.API.Controllers.Borrowing;

[ApiController]
[Route("api/borrow-extensions")]
[Produces("application/json")]
public class BorrowExtensionsController : ControllerBase
{
    private readonly IBorrowExtensionService _extensionService;

    public BorrowExtensionsController(IBorrowExtensionService extensionService)
    {
        _extensionService = extensionService;
    }

    /// <summary>
    /// Nhóm trưởng (người gửi đơn) xin gia hạn đơn đang mượn, phải xin trước khi quá hạn. Mỗi lần tối đa số ngày cấu hình
    /// trong system_settings (BORROW_EXTENSION_MAX_DAYS),
    /// mỗi đơn tối đa số lần được duyệt cấu hình trong BORROW_EXTENSION_MAX_TIMES, mỗi lúc chỉ 1 yêu cầu chờ duyệt. Gửi mail cho giảng viên của lớp.
    /// </summary>
    [HttpPost("~/api/borrow-requests/{id:guid}/extensions")]
    [Authorize(Policy = PermissionCodes.BorrowCreate)]
    [ProducesResponseType(typeof(ApiResponse<BorrowExtensionResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestExtension(Guid id, [FromBody] RequestExtensionRequest request, CancellationToken ct)
    {
        var result = await _extensionService.RequestAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<BorrowExtensionResponse>.Ok(result, "Đã gửi yêu cầu gia hạn"));
    }

    /// <summary>
    /// Danh sách yêu cầu gia hạn cho giảng viên: các đơn thuộc lớp mình dạy (Admin thấy tất cả). Mới nhất lên trước.
    /// Lọc theo trạng thái (Pending / Approved / Rejected), tìm theo mã đơn, tên project, tên sinh viên.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.BorrowApprove)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BorrowExtensionResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExtensions(
        [FromQuery] BorrowExtensionQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _extensionService.GetExtensionsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<BorrowExtensionResponse>>.Ok(result));
    }

    /// <summary>Giảng viên của lớp (hoặc Admin) duyệt: hạn trả của đơn đổi sang ngày mới. Ghi chú không bắt buộc.</summary>
    [HttpPost("{extensionId:guid}/approve")]
    [Authorize(Policy = PermissionCodes.BorrowApprove)]
    [ProducesResponseType(typeof(ApiResponse<BorrowExtensionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        Guid extensionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApproveExtensionRequest? request,
        CancellationToken ct)
    {
        var result = await _extensionService.ApproveAsync(extensionId, request ?? new ApproveExtensionRequest(), ct);
        return Ok(ApiResponse<BorrowExtensionResponse>.Ok(result, "Đã duyệt gia hạn"));
    }

    /// <summary>Giảng viên của lớp (hoặc Admin) từ chối, bắt buộc ghi lý do; lý do được gửi mail cho sinh viên.</summary>
    [HttpPost("{extensionId:guid}/reject")]
    [Authorize(Policy = PermissionCodes.BorrowApprove)]
    [ProducesResponseType(typeof(ApiResponse<BorrowExtensionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid extensionId, [FromBody] RejectExtensionRequest request, CancellationToken ct)
    {
        var result = await _extensionService.RejectAsync(extensionId, request, ct);
        return Ok(ApiResponse<BorrowExtensionResponse>.Ok(result, "Đã từ chối gia hạn"));
    }
}

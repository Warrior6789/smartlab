using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;
using SmartLab.BLL.Interfaces.Identity;

namespace SmartLab.API.Controllers.Identity;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
[Authorize(Policy = PermissionCodes.UserManage)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Danh sách người dùng (phân trang, tìm kiếm, lọc theo role/trạng thái).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] UserQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _userService.GetUsersAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<UserItemResponse>>.Ok(result));
    }

    /// <summary>Chi tiết người dùng.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(id, ct);
        return Ok(ApiResponse<UserDetailResponse>.Ok(user));
    }

    /// <summary>Tạo tài khoản LabStaff/InventoryManager/Lecturer.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var user = await _userService.CreateUserAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<UserDetailResponse>.Ok(user, "Tạo tài khoản thành công"));
    }

    /// <summary>Gán role cho người dùng (thay toàn bộ role hiện tại).</summary>
    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest request, CancellationToken ct)
    {
        var user = await _userService.AssignRolesAsync(id, request, ct);
        return Ok(ApiResponse<UserDetailResponse>.Ok(user, "Cập nhật role thành công"));
    }

    /// <summary>Sửa họ tên, email, số điện thoại của người dùng (username không đổi).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var user = await _userService.UpdateUserAsync(id, request, ct);
        return Ok(ApiResponse<UserDetailResponse>.Ok(user, "Cập nhật người dùng thành công"));
    }

    /// <summary>
    /// Đặt lại mật khẩu ngẫu nhiên cho người dùng (không áp dụng cho Admin): gửi qua email và trả trong response
    /// để Admin đưa trực tiếp. Mọi phiên đăng nhập cũ của người dùng hết hiệu lực.
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(typeof(ApiResponse<ResetUserPasswordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct)
    {
        var result = await _userService.ResetPasswordAsync(id, ct);
        return Ok(ApiResponse<ResetUserPasswordResponse>.Ok(result, "Đặt lại mật khẩu thành công"));
    }
}

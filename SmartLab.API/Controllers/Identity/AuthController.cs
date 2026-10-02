using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Identity;
using SmartLab.BLL.Interfaces.Identity;

namespace SmartLab.API.Controllers.Identity;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Đăng ký tài khoản sinh viên.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var user = await _authService.RegisterStudentAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CurrentUserDto>.Ok(user, "Đăng ký thành công. Vui lòng kiểm tra email để lấy mã xác thực"));
    }

    /// <summary>Xác thực email bằng mã 6 số đã gửi khi đăng ký.</summary>
    [AllowAnonymous]
    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        await _authService.VerifyEmailAsync(request, ct);
        return Ok(ApiResponse.Ok("Xác thực email thành công"));
    }

    /// <summary>Gửi lại mã xác thực email.</summary>
    [AllowAnonymous]
    [HttpPost("resend-verification")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        await _authService.ResendVerificationAsync(request, ct);
        return Ok(ApiResponse.Ok("Nếu email cần xác thực, mã mới đã được gửi"));
    }

    /// <summary>Gửi mã đặt lại mật khẩu tới email.</summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _authService.ForgotPasswordAsync(request, ct);
        return Ok(ApiResponse.Ok("Nếu email tồn tại, mã đặt lại mật khẩu đã được gửi"));
    }

    /// <summary>Đặt lại mật khẩu bằng mã đã gửi tới email.</summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _authService.ResetPasswordAsync(request, ct);
        return Ok(ApiResponse.Ok("Đặt lại mật khẩu thành công"));
    }

    /// <summary>Đăng nhập bằng email hoặc username.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Đăng nhập thành công"));
    }

    /// <summary>Thông tin người dùng hiện tại + roles + permissions.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await _authService.GetCurrentUserAsync(ct);
        return Ok(ApiResponse<CurrentUserDto>.Ok(user));
    }

    /// <summary>Cập nhật thông tin cá nhân của người dùng hiện tại.</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await _authService.UpdateProfileAsync(request, ct);
        return Ok(ApiResponse<CurrentUserDto>.Ok(user, "Cập nhật thông tin thành công"));
    }
}

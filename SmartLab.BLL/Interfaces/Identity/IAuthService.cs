using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Interfaces.Identity;

public interface IAuthService
{
    /// <summary>Logs in by email or username, updates last_login_at and returns a JWT.</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Profile, roles and permissions of the currently authenticated user.</summary>
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken ct = default);

    /// <summary>Updates full name, phone number and avatar of the currently authenticated user.</summary>
    Task<CurrentUserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Changes the password of the currently authenticated user after checking the current one. Every old token
    /// (other devices included) stops working; the returned token keeps this device logged in.
    /// </summary>
    Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>Ends every session of the current user: all tokens issued so far stop working.</summary>
    Task LogoutAsync(CancellationToken ct = default);

    /// <summary>Emails a password reset code. Does nothing for unknown emails, so accounts can't be probed.</summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    /// <summary>Sets a new password when the reset code is correct; every old token stops working.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}

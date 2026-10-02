using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Interfaces.Identity;

public interface IAuthService
{
    /// <summary>Registers a student: users + student_profiles + role Student in one transaction.</summary>
    Task<CurrentUserDto> RegisterStudentAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Logs in by email or username, updates last_login_at and returns a JWT.</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Profile, roles and permissions of the currently authenticated user.</summary>
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken ct = default);

    /// <summary>Updates full name, phone number and avatar of the currently authenticated user.</summary>
    Task<CurrentUserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);

    /// <summary>Marks the email as verified when the code sent at registration is correct.</summary>
    Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default);

    /// <summary>Sends a new verification code. Does nothing for unknown or already verified emails.</summary>
    Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct = default);

    /// <summary>Emails a password reset code. Does nothing for unknown emails, so accounts can't be probed.</summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    /// <summary>Sets a new password when the reset code is correct.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}

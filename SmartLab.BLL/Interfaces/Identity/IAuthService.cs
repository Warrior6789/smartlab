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
}

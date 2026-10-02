using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.External.Email;
using SmartLab.BLL.External.Jwt;
using SmartLab.BLL.External.Security;
using SmartLab.BLL.Interfaces.Identity;
using SmartLab.BLL.Mappings.Identity;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Identity;

public class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "Email/username hoặc mật khẩu không đúng";
    private const string InvalidCodeMessage = "Mã xác thực không hợp lệ hoặc đã hết hạn";
    private const int MaxCodeAttempts = 5;
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailSender _email;

    public AuthService(
        IUnitOfWork uow, IPasswordHasher passwordHasher, IJwtService jwtService, ICurrentUserService currentUser,
        IEmailSender email)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _currentUser = currentUser;
        _email = email;
    }

    public async Task<CurrentUserDto> RegisterStudentAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var studentCode = request.StudentCode.Trim().ToUpperInvariant();

        var users = _uow.Repository<User>();
        var errors = new Dictionary<string, string[]>();
        if (await users.QueryNoTracking().AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct))
            errors["username"] = new[] { "Username đã được sử dụng" };
        if (await users.QueryNoTracking().AnyAsync(u => u.Email.ToLower() == email, ct))
            errors["email"] = new[] { "Email đã được sử dụng" };
        if (await _uow.Repository<StudentProfile>().QueryNoTracking().AnyAsync(s => s.StudentCode == studentCode, ct))
            errors["studentCode"] = new[] { "Mã sinh viên đã tồn tại" };
        if (errors.Count > 0)
            throw new ConflictException("Thông tin đăng ký bị trùng", errors);

        var studentRole = await _uow.Repository<Role>().QueryNoTracking()
                              .FirstOrDefaultAsync(r => r.RoleName == RoleNames.Student, ct)
                          ?? throw new InvalidOperationException($"Role '{RoleNames.Student}' chưa được seed trong database");

        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            StudentProfile = new StudentProfile
            {
                StudentCode = studentCode,
                Major = string.IsNullOrWhiteSpace(request.Major) ? null : request.Major.Trim(),
                Cohort = string.IsNullOrWhiteSpace(request.Cohort) ? null : request.Cohort.Trim(),
            },
        };
        user.UserRoles.Add(new UserRole { RoleId = studentRole.RoleId, AssignedAt = now });
        var code = AddVerificationCode(user, VerificationPurposes.EmailVerification, now);

        try
        {
            await users.AddAsync(user, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Username, email hoặc mã sinh viên đã được sử dụng");
        }

        await SendCodeEmailAsync(user, VerificationPurposes.EmailVerification, code, ct);

        var created = await LoadUserWithAccessAsync(user.UserId, ct);
        return UserMappings.ToCurrentUserDto(created!);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var identifier = request.Identifier.Trim();

        var query = QueryUserWithAccess(tracking: true);
        query = identifier.Contains('@')
            ? query.Where(u => u.Email.ToLower() == identifier.ToLowerInvariant())
            : query.Where(u => u.Username.ToLower() == identifier.ToLower());
        var user = await query.FirstOrDefaultAsync(ct);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException(InvalidCredentialsMessage);

        if (!user.IsActive)
            throw new ForbiddenException("Tài khoản đã bị khóa");

        if (!user.EmailVerified)
            throw new ForbiddenException("Email chưa được xác thực. Vui lòng nhập mã đã gửi tới email của bạn");

        var now = DateTime.UtcNow;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await _uow.SaveChangesAsync(ct);

        var dto = UserMappings.ToCurrentUserDto(user);
        var token = _jwtService.GenerateAccessToken(user, dto.Roles, dto.Permissions);

        return new AuthResponse { AccessToken = token, User = dto };
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var user = await LoadUserWithAccessAsync(userId, ct) ?? throw new UnauthorizedException("Tài khoản không còn tồn tại");
        if (!user.IsActive)
            throw new ForbiddenException("Tài khoản đã bị khóa");

        return UserMappings.ToCurrentUserDto(user);
    }

    public async Task<CurrentUserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var user = await QueryUserWithAccess(tracking: true).FirstOrDefaultAsync(u => u.UserId == userId, ct)
                   ?? throw new UnauthorizedException("Tài khoản không còn tồn tại");
        if (!user.IsActive)
            throw new ForbiddenException("Tài khoản đã bị khóa");

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return UserMappings.ToCurrentUserDto(user);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _uow.Repository<User>().Query().FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct)
                   ?? throw new BadRequestException(InvalidCodeMessage);
        if (user.EmailVerified)
            throw new BadRequestException("Email đã được xác thực");

        var now = DateTime.UtcNow;
        await ConsumeCodeAsync(user.UserId, VerificationPurposes.EmailVerification, request.Code, now, ct);

        user.EmailVerified = true;
        user.UpdatedAt = now;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _uow.Repository<User>().QueryNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        if (user is null || user.EmailVerified || !user.IsActive)
            return;

        await IssueCodeAsync(user, VerificationPurposes.EmailVerification, ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _uow.Repository<User>().QueryNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        if (user is null || !user.IsActive)
            return;

        await IssueCodeAsync(user, VerificationPurposes.PasswordReset, ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _uow.Repository<User>().Query().FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        if (user is null || !user.IsActive)
            throw new BadRequestException(InvalidCodeMessage);

        var now = DateTime.UtcNow;
        await ConsumeCodeAsync(user.UserId, VerificationPurposes.PasswordReset, request.Code, now, ct);

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.EmailVerified = true;
        user.UpdatedAt = now;
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Invalidates the user's open codes for <paramref name="purpose"/>, saves a new one and emails it.
    /// Rejects the request when the last code was sent less than <see cref="ResendCooldown"/> ago.
    /// </summary>
    private async Task IssueCodeAsync(User user, string purpose, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var codes = _uow.Repository<VerificationCode>();

        var openCodes = await codes.Query()
            .Where(c => c.UserId == user.UserId && c.Purpose == purpose && c.UsedAt == null)
            .ToListAsync(ct);
        var lastSentAt = openCodes.Count == 0 ? (DateTime?)null : openCodes.Max(c => c.CreatedAt);
        if (lastSentAt + ResendCooldown > now)
            throw new BadRequestException($"Vui lòng đợi {(int)ResendCooldown.TotalSeconds} giây trước khi yêu cầu mã mới");

        foreach (var open in openCodes)
            open.UsedAt = now;

        var code = GenerateCode();
        await codes.AddAsync(new VerificationCode
        {
            UserId = user.UserId,
            Purpose = purpose,
            CodeHash = _passwordHasher.Hash(code),
            ExpiresAt = now + CodeLifetime,
            CreatedAt = now,
        }, ct);
        await _uow.SaveChangesAsync(ct);

        await SendCodeEmailAsync(user, purpose, code, ct);
    }

    /// <summary>Adds a new code through the user's navigation, so it is saved together with the user.</summary>
    private string AddVerificationCode(User user, string purpose, DateTime now)
    {
        var code = GenerateCode();
        user.VerificationCodes.Add(new VerificationCode
        {
            Purpose = purpose,
            CodeHash = _passwordHasher.Hash(code),
            ExpiresAt = now + CodeLifetime,
            CreatedAt = now,
        });
        return code;
    }

    /// <summary>
    /// Checks <paramref name="code"/> against the user's latest open code and marks it used.
    /// A wrong code counts an attempt (saved immediately); after <see cref="MaxCodeAttempts"/> the code is dead.
    /// The caller must still call SaveChangesAsync to persist UsedAt together with its own changes.
    /// </summary>
    private async Task ConsumeCodeAsync(Guid userId, string purpose, string code, DateTime now, CancellationToken ct)
    {
        var latest = await _uow.Repository<VerificationCode>().Query()
            .Where(c => c.UserId == userId && c.Purpose == purpose && c.UsedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (latest is null || latest.ExpiresAt <= now)
            throw new BadRequestException(InvalidCodeMessage);
        if (latest.AttemptCount >= MaxCodeAttempts)
            throw new BadRequestException("Nhập sai quá nhiều lần. Vui lòng yêu cầu mã mới");

        if (!_passwordHasher.Verify(code.Trim(), latest.CodeHash))
        {
            latest.AttemptCount++;
            await _uow.SaveChangesAsync(ct);
            throw new BadRequestException("Mã xác thực không đúng");
        }

        latest.UsedAt = now;
    }

    private static string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private Task SendCodeEmailAsync(User user, string purpose, string code, CancellationToken ct)
    {
        string subject;
        string action;
        if (purpose == VerificationPurposes.PasswordReset)
        {
            subject = "SmartLab - Mã đặt lại mật khẩu";
            action = "đặt lại mật khẩu";
        }
        else
        {
            subject = "SmartLab - Mã xác thực email";
            action = "xác thực email";
        }

        var body =
            $"<p>Xin chào <b>{WebUtility.HtmlEncode(user.FullName)}</b>,</p>" +
            $"<p>Mã {action} của bạn là:</p>" +
            $"<p style=\"font-size:24px;font-weight:bold;letter-spacing:4px\">{code}</p>" +
            $"<p>Mã có hiệu lực trong {(int)CodeLifetime.TotalMinutes} phút. Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>";

        return _email.SendAsync(user.Email, subject, body, ct);
    }

    private IQueryable<User> QueryUserWithAccess(bool tracking)
    {
        var repo = _uow.Repository<User>();
        return (tracking ? repo.Query() : repo.QueryNoTracking())
            .Include(u => u.StudentProfile)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.Permissions)
            .AsSplitQuery();
    }

    private Task<User?> LoadUserWithAccessAsync(Guid userId, CancellationToken ct)
        => QueryUserWithAccess(tracking: false).FirstOrDefaultAsync(u => u.UserId == userId, ct);
}

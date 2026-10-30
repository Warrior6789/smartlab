using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.External.Email;
using SmartLab.BLL.External.Jwt;
using SmartLab.BLL.External.Security;
using SmartLab.BLL.Interfaces.AuditLogs;
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
    private readonly IActiveUserChecker _sessionChecker;
    private readonly IAuditLogger _auditLogger;

    public AuthService(
        IUnitOfWork uow, IPasswordHasher passwordHasher, IJwtService jwtService, ICurrentUserService currentUser,
        IEmailSender email, IActiveUserChecker sessionChecker, IAuditLogger auditLogger)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _currentUser = currentUser;
        _email = email;
        _sessionChecker = sessionChecker;
        _auditLogger = auditLogger;
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
        {
            await LogFailedLoginAsync(user, identifier, "Sai email/username hoặc mật khẩu", ct);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (!user.IsActive)
        {
            await LogFailedLoginAsync(user, identifier, "Tài khoản đã bị khóa", ct);
            throw new ForbiddenException("Tài khoản đã bị khóa");
        }

        var now = DateTime.UtcNow;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await _auditLogger.AddAsync(AuditActions.Login, nameof(User), user.UserId.ToString(), userId: user.UserId, ct: ct);
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

    public async Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var user = await QueryUserWithAccess(tracking: true).FirstOrDefaultAsync(u => u.UserId == userId, ct)
                   ?? throw new UnauthorizedException("Tài khoản không còn tồn tại");
        if (!user.IsActive)
            throw new ForbiddenException("Tài khoản đã bị khóa");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new BadRequestException("Mật khẩu hiện tại không đúng");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.TokenVersion++;
        user.UpdatedAt = DateTime.UtcNow;
        await _auditLogger.AddAsync(AuditActions.ChangePassword, nameof(User), user.UserId.ToString(), ct: ct);
        await _uow.SaveChangesAsync(ct);
        _sessionChecker.Forget(userId);

        var dto = UserMappings.ToCurrentUserDto(user);
        var token = _jwtService.GenerateAccessToken(user, dto.Roles, dto.Permissions);

        return new AuthResponse { AccessToken = token, User = dto };
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        await _uow.Repository<User>().Query()
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.TokenVersion, u => u.TokenVersion + 1)
                .SetProperty(u => u.UpdatedAt, DateTime.UtcNow), ct);
        _sessionChecker.Forget(userId);
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
        user.TokenVersion++;
        user.UpdatedAt = now;
        await _uow.SaveChangesAsync(ct);
        _sessionChecker.Forget(user.UserId);
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

        await SendCodeEmailAsync(user, code, ct);
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

    private Task SendCodeEmailAsync(User user, string code, CancellationToken ct)
    {
        var body =
            $"<p>Xin chào <b>{WebUtility.HtmlEncode(user.FullName)}</b>,</p>" +
            $"<p>Mã đặt lại mật khẩu của bạn là:</p>" +
            $"<p style=\"font-size:24px;font-weight:bold;letter-spacing:4px\">{code}</p>" +
            $"<p>Mã có hiệu lực trong {(int)CodeLifetime.TotalMinutes} phút. Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>";

        return _email.SendAsync(user.Email, "SmartLab - Mã đặt lại mật khẩu", body, ct);
    }

    /// <summary>Saves a failed LOGIN row; EntityId is the user id, or the typed identifier when no account matches.</summary>
    private async Task LogFailedLoginAsync(User? user, string identifier, string reason, CancellationToken ct)
    {
        Guid? userId = null;
        var entityId = identifier;
        if (user != null)
        {
            userId = user.UserId;
            entityId = user.UserId.ToString();
        }

        await _auditLogger.AddAsync(AuditActions.Login, nameof(User), entityId,
            newValue: new { Identifier = identifier, Reason = reason }, isSuccess: false, userId: userId, ct: ct);
        await _uow.SaveChangesAsync(ct);
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

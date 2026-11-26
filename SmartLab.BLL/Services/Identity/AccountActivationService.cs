using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;
using SmartLab.BLL.External.Email;
using SmartLab.BLL.External.Security;
using SmartLab.BLL.Interfaces.AuditLogs;
using SmartLab.BLL.Interfaces.Identity;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Identity;

public class AccountActivationService : IAccountActivationService
{
    private const string InvalidLinkMessage = "Link kích hoạt không đúng hoặc đã hết hạn, vui lòng nhờ Admin gửi lại email";
    private const int TokenBytes = 32;
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailSender _email;
    private readonly IActiveUserChecker _sessionChecker;
    private readonly IChangeBroadcaster _broadcaster;
    private readonly IAuditLogger _auditLogger;
    private readonly WebOptions _web;

    public AccountActivationService(
        IUnitOfWork uow, IPasswordHasher passwordHasher, IEmailSender email, IActiveUserChecker sessionChecker,
        IChangeBroadcaster broadcaster, IAuditLogger auditLogger, IOptions<WebOptions> web)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _email = email;
        _sessionChecker = sessionChecker;
        _broadcaster = broadcaster;
        _auditLogger = auditLogger;
        _web = web.Value;
    }

    public string Prepare(User user)
    {
        var token = GenerateToken();
        user.IsActive = false;
        user.VerificationCodes.Add(NewCode(token, DateTime.UtcNow));
        return token;
    }

    public Task SendEmailAsync(User user, string password, string token, CancellationToken ct = default)
    {
        var link = $"{_web.BaseUrl.TrimEnd('/')}/activate#{token}";
        var body =
            $"<p>Xin chào <b>{WebUtility.HtmlEncode(user.FullName)}</b>,</p>" +
            "<p>Tài khoản SmartLab của bạn đã được tạo:</p>" +
            $"<p>Tên đăng nhập: <b>{WebUtility.HtmlEncode(user.Username)}</b><br/>" +
            $"Mật khẩu: <b>{WebUtility.HtmlEncode(password)}</b></p>" +
            "<p>Bạn cần kích hoạt tài khoản trước khi đăng nhập:</p>" +
            $"<p><a href=\"{link}\" style=\"display:inline-block;padding:12px 24px;background:#0058be;color:#ffffff;" +
            "text-decoration:none;border-radius:6px;font-weight:bold\">Kích hoạt tài khoản</a></p>" +
            $"<p>Link có hiệu lực trong {(int)LinkLifetime.TotalDays} ngày. Quá hạn thì nhờ Admin gửi lại email.</p>" +
            "<p>Bạn có thể đổi mật khẩu trong trang cá nhân sau khi đăng nhập.</p>";

        return _email.SendAsync(user.Email, "SmartLab - Kích hoạt tài khoản của bạn", body, ct);
    }

    public async Task<ActivateAccountResponse> ActivateAsync(ActivateAccountRequest request, CancellationToken ct = default)
    {
        var hash = HashToken(request.Token.Trim());
        var link = await _uow.Repository<VerificationCode>().QueryNoTracking()
                       .Where(c => c.Purpose == VerificationPurposes.EmailVerification && c.CodeHash == hash)
                       .Select(c => new { c.VerificationCodeId, c.UserId, c.UsedAt, c.User.Username, c.User.FullName, c.User.IsActive })
                       .FirstOrDefaultAsync(ct)
                   ?? throw new BadRequestException(InvalidLinkMessage);

        var response = new ActivateAccountResponse { Username = link.Username, FullName = link.FullName };
        if (link.UsedAt != null && link.IsActive)
            return response;

        var now = DateTime.UtcNow;
        await using (var tx = await _uow.BeginTransactionAsync(ct))
        {
            await LockUserAsync(link.UserId, ct);

            var used = await _uow.Repository<VerificationCode>().Query()
                .Where(c => c.VerificationCodeId == link.VerificationCodeId && c.UsedAt == null && c.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, now), ct);
            if (used == 0)
            {
                var activatedMeanwhile = await _uow.Repository<User>().QueryNoTracking()
                    .AnyAsync(u => u.UserId == link.UserId && u.IsActive, ct);
                if (activatedMeanwhile)
                    return response;

                throw new BadRequestException(InvalidLinkMessage);
            }

            await _uow.Repository<User>().Query()
                .Where(u => u.UserId == link.UserId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.IsActive, true)
                    .SetProperty(u => u.UpdatedAt, now), ct);
            await tx.CommitAsync(ct);
        }

        _sessionChecker.Forget(link.UserId);
        await _broadcaster.AccountChangedAsync(link.UserId);

        return response;
    }

    public async Task ResendAsync(Guid userId, CancellationToken ct = default)
    {
        var password = PasswordGenerator.Generate();
        var token = GenerateToken();
        User user;

        await using (var tx = await _uow.BeginTransactionAsync(ct))
        {
            await LockUserAsync(userId, ct);

            user = await _uow.Repository<User>().Query()
                       .Include(u => u.VerificationCodes
                           .Where(c => c.Purpose == VerificationPurposes.EmailVerification && c.UsedAt == null))
                       .FirstOrDefaultAsync(u => u.UserId == userId, ct)
                   ?? throw new NotFoundException("Không tìm thấy người dùng");
            if (user.IsActive)
                throw new ConflictException("Tài khoản đã được kích hoạt");

            var openCodes = user.VerificationCodes.ToList();
            if (openCodes.Count == 0)
                throw new ConflictException("Tài khoản đã bị khóa, không gửi lại email kích hoạt được");

            var now = DateTime.UtcNow;
            if (openCodes.Max(c => c.CreatedAt) + ResendCooldown > now)
                throw new BadRequestException($"Vui lòng đợi {(int)ResendCooldown.TotalSeconds} giây trước khi gửi lại");

            foreach (var open in openCodes)
                open.UsedAt = now;
            user.VerificationCodes.Add(NewCode(token, now));
            user.PasswordHash = _passwordHasher.Hash(password);
            user.UpdatedAt = now;

            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        await SendEmailAsync(user, password, token, ct);
    }

    public async Task ActivateByAdminAsync(Guid userId, ActivateUserRequest request, CancellationToken ct = default)
    {
        await using (var tx = await _uow.BeginTransactionAsync(ct))
        {
            await LockUserAsync(userId, ct);

            var user = await _uow.Repository<User>().Query()
                           .Include(u => u.VerificationCodes
                               .Where(c => c.Purpose == VerificationPurposes.EmailVerification && c.UsedAt == null))
                           .FirstOrDefaultAsync(u => u.UserId == userId, ct)
                       ?? throw new NotFoundException("Không tìm thấy người dùng");
            if (user.IsActive)
                throw new ConflictException("Tài khoản đã được kích hoạt");

            var openCodes = user.VerificationCodes.ToList();
            if (openCodes.Count == 0)
                throw new ConflictException("Tài khoản đã bị khóa, không kích hoạt thủ công được");

            var now = DateTime.UtcNow;
            foreach (var open in openCodes)
                open.UsedAt = now;
            user.IsActive = true;
            user.PasswordHash = _passwordHasher.Hash(request.TemporaryPassword);
            user.MustChangePassword = true;
            user.TokenVersion++;
            user.UpdatedAt = now;
            await _auditLogger.AddAsync(AuditActions.ActivateUser, nameof(User), user.UserId.ToString(), ct: ct);

            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        _sessionChecker.Forget(userId);
        await _broadcaster.AccountChangedAsync(userId);
    }

    /// <summary>Locks the user row (no-op UPDATE) so activating and resending at the same time run one after the other.</summary>
    private Task LockUserAsync(Guid userId, CancellationToken ct)
        => _uow.Repository<User>().Query()
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, u => u.IsActive), ct);

    private static VerificationCode NewCode(string token, DateTime now) => new()
    {
        Purpose = VerificationPurposes.EmailVerification,
        CodeHash = HashToken(token),
        ExpiresAt = now + LinkLifetime,
        CreatedAt = now,
    };

    private static string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes)).ToLowerInvariant();

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

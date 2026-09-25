using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;
using SmartLab.BLL.External.CurrentUser;
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

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly ICurrentUserService _currentUser;

    public AuthService(
        IUnitOfWork uow, IPasswordHasher passwordHasher, IJwtService jwtService, ICurrentUserService currentUser)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _currentUser = currentUser;
    }

    public async Task<CurrentUserDto> RegisterStudentAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        var studentCode = request.StudentCode.Trim().ToUpperInvariant();

        var users = _uow.Repository<User>();
        var errors = new Dictionary<string, string[]>();
        if (await users.QueryNoTracking().AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct))
            errors["username"] = new[] { "Username đã được sử dụng" };
        if (await users.QueryNoTracking().AnyAsync(u => u.Email == email, ct))
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

        try
        {
            await users.AddAsync(user, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Username, email hoặc mã sinh viên đã được sử dụng");
        }

        return (await LoadUserWithAccessAsync(user.UserId, ct))!.ToCurrentUserDto();
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var identifier = request.Identifier.Trim();

        var query = QueryUserWithAccess(tracking: true);
        query = identifier.Contains('@')
            ? query.Where(u => u.Email == identifier)
            : query.Where(u => u.Username.ToLower() == identifier.ToLower());
        var user = await query.FirstOrDefaultAsync(ct);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException(InvalidCredentialsMessage);

        if (!user.IsActive)
            throw new ForbiddenException("Tài khoản đã bị khóa");

        var now = DateTime.UtcNow;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await _uow.SaveChangesAsync(ct);

        var dto = user.ToCurrentUserDto();
        var token = _jwtService.GenerateAccessToken(user, dto.Roles, dto.Permissions);

        return new AuthResponse { AccessToken = token, User = dto };
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var user = await LoadUserWithAccessAsync(userId, ct) ?? throw new UnauthorizedException("Tài khoản không còn tồn tại");
        if (!user.IsActive)
            throw new ForbiddenException("Tài khoản đã bị khóa");

        return user.ToCurrentUserDto();
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

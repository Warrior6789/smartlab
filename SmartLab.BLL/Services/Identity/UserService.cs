using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;
using SmartLab.BLL.External.Security;
using SmartLab.BLL.Interfaces.Identity;
using SmartLab.BLL.Mappings.Identity;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Identity;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUnitOfWork uow, IPasswordHasher passwordHasher)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDetailResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var isLecturer = request.Role == RoleNames.Lecturer;
        var instructorCode = isLecturer ? request.InstructorCode!.Trim().ToUpperInvariant() : null;

        var users = _uow.Repository<User>();
        var errors = new Dictionary<string, string[]>();
        if (await users.QueryNoTracking().AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct))
            errors["username"] = new[] { "Username đã được sử dụng" };
        if (await users.QueryNoTracking().AnyAsync(u => u.Email.ToLower() == email, ct))
            errors["email"] = new[] { "Email đã được sử dụng" };
        if (isLecturer && await _uow.Repository<InstructorProfile>().QueryNoTracking()
                .AnyAsync(i => i.InstructorCode == instructorCode, ct))
            errors["instructorCode"] = new[] { "Mã giảng viên đã tồn tại" };
        if (errors.Count > 0)
            throw new ConflictException("Thông tin tài khoản bị trùng", errors);

        var role = await _uow.Repository<Role>().QueryNoTracking()
                       .FirstOrDefaultAsync(r => r.RoleName == request.Role, ct)
                   ?? throw new InvalidOperationException($"Role '{request.Role}' chưa được seed trong database");

        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            IsActive = true,
            EmailVerified = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.UserRoles.Add(new UserRole { RoleId = role.RoleId, AssignedAt = now });

        if (isLecturer)
        {
            user.InstructorProfile = new InstructorProfile
            {
                InstructorCode = instructorCode!,
                Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim(),
                AcademicTitle = string.IsNullOrWhiteSpace(request.AcademicTitle) ? null : request.AcademicTitle.Trim(),
            };
        }

        try
        {
            await users.AddAsync(user, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Username, email hoặc mã giảng viên đã được sử dụng");
        }

        return await GetUserByIdAsync(user.UserId, ct);
    }

    public async Task<UserDetailResponse> AssignRolesAsync(
        Guid userId, AssignRolesRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Repository<User>().Query()
                       .Include(u => u.StudentProfile)
                       .Include(u => u.InstructorProfile)
                       .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                       .AsSplitQuery()
                       .FirstOrDefaultAsync(u => u.UserId == userId, ct)
                   ?? throw new NotFoundException("Không tìm thấy người dùng");

        if (user.UserRoles.Any(ur => ur.Role.RoleName == RoleNames.Admin))
            throw new ForbiddenException("Không thể thay đổi role của tài khoản Admin");

        if (request.Roles.Contains(RoleNames.Lecturer) && user.InstructorProfile is null)
            throw new BadRequestException("User chưa có hồ sơ giảng viên nên không thể gán role Lecturer");

        if (request.Roles.Contains(RoleNames.Student) && user.StudentProfile is null)
            throw new BadRequestException("User chưa có hồ sơ sinh viên nên không thể gán role Student");

        var newRoles = await _uow.Repository<Role>().QueryNoTracking()
            .Where(r => request.Roles.Contains(r.RoleName))
            .ToListAsync(ct);
        if (newRoles.Count != request.Roles.Count)
            throw new InvalidOperationException("Có role chưa được seed trong database");

        var userRoles = _uow.Repository<UserRole>();
        foreach (var old in user.UserRoles.Where(ur => !request.Roles.Contains(ur.Role.RoleName)).ToList())
            userRoles.Remove(old);

        var now = DateTime.UtcNow;
        foreach (var role in newRoles.Where(r => user.UserRoles.All(ur => ur.RoleId != r.RoleId)))
            user.UserRoles.Add(new UserRole { RoleId = role.RoleId, AssignedAt = now });

        user.UpdatedAt = now;
        await _uow.SaveChangesAsync(ct);

        return await GetUserByIdAsync(user.UserId, ct);
    }

    public Task<PagedResult<UserItemResponse>> GetUsersAsync(
        UserQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var users = _uow.Repository<User>().QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            users = users.Where(u => u.Username.ToLower().Contains(keyword)
                                  || u.Email.ToLower().Contains(keyword)
                                  || u.FullName.ToLower().Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = query.Role.Trim();
            users = users.Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == role));
        }

        if (query.IsActive.HasValue)
            users = users.Where(u => u.IsActive == query.IsActive.Value);

        var result = users
            .OrderByDescending(u => u.CreatedAt).ThenBy(u => u.UserId)
            .Select(u => new UserItemResponse
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email,
                FullName = u.FullName,
                AvatarUrl = u.AvatarUrl,
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt,
                Roles = u.UserRoles.Select(ur => ur.Role.RoleName).OrderBy(r => r).ToList(),
            });

        return PagedResult<UserItemResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<UserDetailResponse> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _uow.Repository<User>().QueryNoTracking()
                       .Include(u => u.StudentProfile)
                       .Include(u => u.InstructorProfile)
                       .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                       .AsSplitQuery()
                       .FirstOrDefaultAsync(u => u.UserId == userId, ct)
                   ?? throw new NotFoundException("Không tìm thấy người dùng");

        return UserMappings.ToDetailResponse(user);
    }
}

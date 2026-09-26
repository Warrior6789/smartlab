using SmartLab.BLL.DTOs.Identity;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Identity;

public static class UserMappings
{
    /// <summary>Requires UserRoles.Role.Permissions (and optionally StudentProfile) to be loaded.</summary>
    public static CurrentUserDto ToCurrentUserDto(User user) => new()
    {
        UserId = user.UserId,
        Username = user.Username,
        Email = user.Email,
        FullName = user.FullName,
        PhoneNumber = user.PhoneNumber,
        AvatarUrl = user.AvatarUrl,
        IsActive = user.IsActive,
        LastLoginAt = user.LastLoginAt,
        StudentCode = user.StudentProfile?.StudentCode,
        Roles = GetRoleNames(user),
        Permissions = GetPermissionCodes(user),
    };

    /// <summary>Requires UserRoles.Role, StudentProfile and InstructorProfile to be loaded.</summary>
    public static UserDetailResponse ToDetailResponse(User user) => new()
    {
        UserId = user.UserId,
        Username = user.Username,
        Email = user.Email,
        FullName = user.FullName,
        AvatarUrl = user.AvatarUrl,
        IsActive = user.IsActive,
        LastLoginAt = user.LastLoginAt,
        CreatedAt = user.CreatedAt,
        Roles = GetRoleNames(user),
        PhoneNumber = user.PhoneNumber,
        UpdatedAt = user.UpdatedAt,
        StudentProfile = user.StudentProfile is null ? null : new StudentProfileResponse
        {
            StudentCode = user.StudentProfile.StudentCode,
            Major = user.StudentProfile.Major,
            Cohort = user.StudentProfile.Cohort,
        },
        InstructorProfile = user.InstructorProfile is null ? null : new InstructorProfileResponse
        {
            InstructorCode = user.InstructorProfile.InstructorCode,
            Department = user.InstructorProfile.Department,
            AcademicTitle = user.InstructorProfile.AcademicTitle,
        },
    };

    public static IReadOnlyList<string> GetRoleNames(User user)
        => user.UserRoles.Select(ur => ur.Role.RoleName).Distinct().OrderBy(r => r).ToList();

    public static IReadOnlyList<string> GetPermissionCodes(User user)
        => user.UserRoles.SelectMany(ur => ur.Role.Permissions).Select(p => p.PermissionCode)
            .Distinct().OrderBy(p => p).ToList();
}

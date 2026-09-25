using SmartLab.BLL.DTOs.Identity;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Identity;

public static class UserMappings
{
    /// <summary>Requires UserRoles.Role.Permissions (and optionally StudentProfile) to be loaded.</summary>
    public static CurrentUserDto ToCurrentUserDto(this User user) => new()
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
        Roles = user.GetRoleNames(),
        Permissions = user.GetPermissionCodes(),
    };

    public static IReadOnlyList<string> GetRoleNames(this User user)
        => user.UserRoles.Select(ur => ur.Role.RoleName).Distinct().OrderBy(r => r).ToList();

    public static IReadOnlyList<string> GetPermissionCodes(this User user)
        => user.UserRoles.SelectMany(ur => ur.Role.Permissions).Select(p => p.PermissionCode)
            .Distinct().OrderBy(p => p).ToList();
}

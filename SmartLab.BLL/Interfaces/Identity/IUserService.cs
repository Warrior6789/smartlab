using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Interfaces.Identity;

public interface IUserService
{
    /// <summary>Paged user list, filtered by search text, role name and active status.</summary>
    Task<PagedResult<UserItemResponse>> GetUsersAsync(UserQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>User detail with roles and student/instructor profile.</summary>
    Task<UserDetailResponse> GetUserByIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Admin creates a LabStaff/InventoryManager/Lecturer account (plus an instructor profile for Lecturer).</summary>
    Task<UserDetailResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);

    /// <summary>
    /// Replaces the user's roles with the given list (Admin accounts cannot be changed). The user's old tokens stop
    /// working so the new roles apply at the next login.
    /// </summary>
    Task<UserDetailResponse> AssignRolesAsync(Guid userId, AssignRolesRequest request, CancellationToken ct = default);

    /// <summary>Admin fixes full name, email and phone number; the email must stay unique.</summary>
    Task<UserDetailResponse> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken ct = default);

    /// <summary>
    /// Admin sets a new random password (not for Admin accounts), emails it and returns it; every old token of the
    /// user stops working.
    /// </summary>
    Task<ResetUserPasswordResponse> ResetPasswordAsync(Guid userId, CancellationToken ct = default);
}

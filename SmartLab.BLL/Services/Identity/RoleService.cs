using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;
using SmartLab.BLL.Interfaces.Identity;
using SmartLab.BLL.Mappings.Identity;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Identity;

public class RoleService : IRoleService
{
    private readonly IUnitOfWork _uow;

    public RoleService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IReadOnlyList<RoleItemResponse>> GetRolesAsync(CancellationToken ct = default)
    {
        return await _uow.Repository<Role>().QueryNoTracking()
            .OrderBy(r => r.RoleName)
            .Select(r => new RoleItemResponse
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                Description = r.Description,
                UserCount = r.UserRoles.Count,
                PermissionCount = r.Permissions.Count,
            })
            .ToListAsync(ct);
    }

    public async Task<RoleDetailResponse> GetRoleByIdAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await LoadRoleAsync(roleId, tracking: false, ct);
        return RoleMappings.ToDetailResponse(role);
    }

    public async Task<IReadOnlyList<PermissionResponse>> GetPermissionsAsync(CancellationToken ct = default)
    {
        return await _uow.Repository<Permission>().QueryNoTracking()
            .OrderBy(p => p.Module).ThenBy(p => p.PermissionCode)
            .Select(p => new PermissionResponse
            {
                PermissionId = p.PermissionId,
                PermissionCode = p.PermissionCode,
                PermissionName = p.PermissionName,
                Module = p.Module,
            })
            .ToListAsync(ct);
    }

    public async Task<RoleDetailResponse> AddPermissionAsync(
        Guid roleId, string permissionCode, CancellationToken ct = default)
    {
        var role = await LoadEditableRoleAsync(roleId, ct);
        var code = permissionCode.Trim().ToUpperInvariant();

        if (role.Permissions.Any(p => p.PermissionCode == code))
            return RoleMappings.ToDetailResponse(role);

        var permission = await _uow.Repository<Permission>().Query()
                             .FirstOrDefaultAsync(p => p.PermissionCode == code, ct)
                         ?? throw new NotFoundException($"Không tìm thấy permission '{code}'");

        role.Permissions.Add(permission);
        await _uow.SaveChangesAsync(ct);

        return RoleMappings.ToDetailResponse(role);
    }

    public async Task<RoleDetailResponse> RemovePermissionAsync(
        Guid roleId, string permissionCode, CancellationToken ct = default)
    {
        var role = await LoadEditableRoleAsync(roleId, ct);
        var code = permissionCode.Trim().ToUpperInvariant();

        var permission = role.Permissions.FirstOrDefault(p => p.PermissionCode == code)
                         ?? throw new NotFoundException($"Role '{role.RoleName}' không có permission '{code}'");

        role.Permissions.Remove(permission);
        await _uow.SaveChangesAsync(ct);

        return RoleMappings.ToDetailResponse(role);
    }

    private async Task<Role> LoadEditableRoleAsync(Guid roleId, CancellationToken ct)
    {
        var role = await LoadRoleAsync(roleId, tracking: true, ct);
        if (role.RoleName == RoleNames.Admin)
            throw new ForbiddenException("Không thể thay đổi permission của role Admin");

        return role;
    }

    private async Task<Role> LoadRoleAsync(Guid roleId, bool tracking, CancellationToken ct)
    {
        var repo = _uow.Repository<Role>();
        return await (tracking ? repo.Query() : repo.QueryNoTracking())
                   .Include(r => r.Permissions)
                   .Include(r => r.UserRoles)
                   .AsSplitQuery()
                   .FirstOrDefaultAsync(r => r.RoleId == roleId, ct)
               ?? throw new NotFoundException("Không tìm thấy role");
    }
}

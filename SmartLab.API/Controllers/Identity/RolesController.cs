using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;
using SmartLab.BLL.Interfaces.Identity;

namespace SmartLab.API.Controllers.Identity;

[ApiController]
[Route("api/roles")]
[Produces("application/json")]
[Authorize(Policy = PermissionCodes.RoleManage)]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <summary>Danh sách role (kèm số user và số permission).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRoles(CancellationToken ct)
    {
        var roles = await _roleService.GetRolesAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<RoleItemResponse>>.Ok(roles));
    }

    /// <summary>Chi tiết role kèm danh sách permission.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRole(Guid id, CancellationToken ct)
    {
        var role = await _roleService.GetRoleByIdAsync(id, ct);
        return Ok(ApiResponse<RoleDetailResponse>.Ok(role));
    }

    /// <summary>Thêm 1 permission cho role (giữ nguyên các permission cũ).</summary>
    [HttpPost("{id:guid}/permissions/{code}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddPermission(Guid id, string code, CancellationToken ct)
    {
        var role = await _roleService.AddPermissionAsync(id, code, ct);
        return Ok(ApiResponse<RoleDetailResponse>.Ok(role, "Thêm permission thành công"));
    }

    /// <summary>Gỡ 1 permission khỏi role.</summary>
    [HttpDelete("{id:guid}/permissions/{code}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePermission(Guid id, string code, CancellationToken ct)
    {
        var role = await _roleService.RemovePermissionAsync(id, code, ct);
        return Ok(ApiResponse<RoleDetailResponse>.Ok(role, "Gỡ permission thành công"));
    }
}

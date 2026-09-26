using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;
using SmartLab.BLL.Interfaces.Identity;

namespace SmartLab.API.Controllers.Identity;

[ApiController]
[Route("api/permissions")]
[Produces("application/json")]
[Authorize(Policy = PermissionCodes.RoleManage)]
public class PermissionsController : ControllerBase
{
    private readonly IRoleService _roleService;

    public PermissionsController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <summary>Danh sách toàn bộ permission (theo module).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PermissionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPermissions(CancellationToken ct)
    {
        var permissions = await _roleService.GetPermissionsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<PermissionResponse>>.Ok(permissions));
    }
}

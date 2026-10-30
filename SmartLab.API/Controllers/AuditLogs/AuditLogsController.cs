using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.AuditLogs;
using SmartLab.BLL.Interfaces.AuditLogs;

namespace SmartLab.API.Controllers.AuditLogs;

[ApiController]
[Route("api/audit-logs")]
[Produces("application/json")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>Nhật ký thao tác: đăng nhập, đổi mật khẩu, tài khoản, role, permission, cài đặt (mới nhất trước, phân trang, lọc theo người làm/hành động/đối tượng/kết quả/ngày).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.AuditLogView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] AuditLogQueryParams query, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _auditLogService.GetLogsAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<AuditLogResponse>>.Ok(result));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Settings;
using SmartLab.BLL.Interfaces.Settings;

namespace SmartLab.API.Controllers.Settings;

[ApiController]
[Route("api/system-settings")]
[Produces("application/json")]
[Authorize(Roles = RoleNames.Admin)]
public class SystemSettingsController : ControllerBase
{
    private readonly ISystemSettingService _settingService;

    public SystemSettingsController(ISystemSettingService settingService)
    {
        _settingService = settingService;
    }

    /// <summary>Các cài đặt hệ thống Admin được sửa (lab nhận đồ, số ngày mượn, gia hạn, giờ nhắc).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SystemSettingResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await _settingService.GetSettingsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<SystemSettingResponse>>.Ok(settings));
    }

    /// <summary>Sửa một cài đặt theo key, ví dụ PICKUP_ROOM_ID hoặc BORROW_MAX_DAYS.</summary>
    [HttpPut("{key}")]
    [ProducesResponseType(typeof(ApiResponse<SystemSettingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSystemSettingRequest request, CancellationToken ct)
    {
        var setting = await _settingService.UpdateSettingAsync(key, request, ct);
        return Ok(ApiResponse<SystemSettingResponse>.Ok(setting, "Cập nhật cài đặt thành công"));
    }
}

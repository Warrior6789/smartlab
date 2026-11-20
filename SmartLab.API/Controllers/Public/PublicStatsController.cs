using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Public;
using SmartLab.BLL.Interfaces.Public;

namespace SmartLab.API.Controllers.Public;

[ApiController]
[Route("api/public")]
[Produces("application/json")]
[AllowAnonymous]
public class PublicStatsController : ControllerBase
{
    private readonly IPublicStatsService _statsService;

    public PublicStatsController(IPublicStatsService statsService)
    {
        _statsService = statsService;
    }

    /// <summary>Số liệu tổng cho trang chủ (không cần đăng nhập): món sẵn sàng, đơn đang mượn, tỉ lệ trả đúng hạn.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<PublicStatsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _statsService.GetStatsAsync(ct);
        return Ok(ApiResponse<PublicStatsResponse>.Ok(result));
    }
}

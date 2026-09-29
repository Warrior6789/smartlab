using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ProjectProposals;
using SmartLab.BLL.Interfaces.ProjectProposals;

namespace SmartLab.API.Controllers.ProjectProposals;

[ApiController]
[Route("api/project-component-options")]
[Produces("application/json")]
[Authorize(Policy = PermissionCodes.ComponentView)]
public class ProjectComponentOptionsController : ControllerBase
{
    private readonly IProjectComponentCatalogService _catalogService;

    public ProjectComponentOptionsController(IProjectComponentCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    /// <summary>Danh sách linh kiện active và số lượng đang khả dụng cho proposal.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProjectComponentOptionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOptions(
        [FromQuery] string? search, [FromQuery] PageParams paging, CancellationToken ct)
    {
        var result = await _catalogService.GetOptionsAsync(search, paging, ct);
        return Ok(ApiResponse<PagedResult<ProjectComponentOptionResponse>>.Ok(result));
    }
}

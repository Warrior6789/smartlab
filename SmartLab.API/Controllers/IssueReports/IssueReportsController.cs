using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartLab.BLL.Common;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.IssueReports;
using SmartLab.BLL.Interfaces.IssueReports;

namespace SmartLab.API.Controllers.IssueReports;

[ApiController]
[Route("api/issue-reports")]
[Produces("application/json")]
public class IssueReportsController : ControllerBase
{
    private const string CreateRoles =
        RoleNames.Student + "," + RoleNames.LabStaff + "," + RoleNames.InventoryManager + "," + RoleNames.Admin;

    private const string ReadRoles =
        CreateRoles + "," + RoleNames.Lecturer;

    private const string ResolveRoles =
        RoleNames.LabStaff + "," + RoleNames.Admin;

    private readonly IIssueReportService _issueReportService;

    public IssueReportsController(IIssueReportService issueReportService)
    {
        _issueReportService = issueReportService;
    }

    /// <summary>Báo một thiết bị REUSABLE/HIGH_VALUE bị hỏng, cháy hoặc mất.</summary>
    [HttpPost]
    [Authorize(Roles = CreateRoles)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<IssueReportDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromForm] CreateIssueReportRequest request,
        CancellationToken ct)
    {
        var report = await _issueReportService.CreateAsync(request, ct);
        return CreatedAtAction(
            nameof(GetById),
            new { id = report.IssueReportId },
            ApiResponse<IssueReportDetailResponse>.Ok(report, "Tạo báo cáo sự cố thành công"));
    }

    /// <summary>LabStaff đúng lab hoặc Admin xác nhận hay từ chối báo cáo đang Open.</summary>
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = ResolveRoles)]
    [Authorize(Policy = PermissionCodes.IssueResolve)]
    [ProducesResponseType(typeof(ApiResponse<IssueReportDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve(
        Guid id,
        [FromBody] ResolveIssueReportRequest request,
        CancellationToken ct)
    {
        var report = await _issueReportService.ResolveAsync(id, request, ct);
        return Ok(ApiResponse<IssueReportDetailResponse>.Ok(report, "Xử lý báo cáo sự cố thành công"));
    }

    /// <summary>Người tạo báo cáo hoặc Admin hủy báo cáo khi còn Open.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IssueReportDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var report = await _issueReportService.CancelAsync(id, ct);
        return Ok(ApiResponse<IssueReportDetailResponse>.Ok(report, "Hủy báo cáo sự cố thành công"));
    }

    /// <summary>Danh sách báo cáo sự cố theo phạm vi role, có phân trang và bộ lọc.</summary>
    [HttpGet]
    [Authorize(Roles = ReadRoles)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<IssueReportListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        [FromQuery] IssueReportQueryParams query,
        [FromQuery] PageParams paging,
        CancellationToken ct)
    {
        var reports = await _issueReportService.GetAsync(query, paging, ct);
        return Ok(ApiResponse<PagedResult<IssueReportListItemResponse>>.Ok(reports));
    }

    /// <summary>Chi tiết một báo cáo sự cố theo phạm vi dữ liệu của người dùng.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = ReadRoles)]
    [ProducesResponseType(typeof(ApiResponse<IssueReportDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var report = await _issueReportService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<IssueReportDetailResponse>.Ok(report));
    }
}
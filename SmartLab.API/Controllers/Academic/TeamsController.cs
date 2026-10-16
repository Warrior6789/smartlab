using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.Interfaces.Academic;

namespace SmartLab.API.Controllers.Academic;

[ApiController]
[Route("api/teams")]
[Produces("application/json")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamsController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    /// <summary>Sinh viên tạo nhóm mới trong lớp (có thể chọn mời luôn các bạn khác).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequest request, CancellationToken ct)
    {
        var result = await _teamService.CreateTeamAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<TeamResponse>.Ok(result, "Tạo nhóm thành công"));
    }

    /// <summary>Lấy danh sách các sinh viên trong lớp chưa có nhóm (phục vụ ô tìm kiếm & nút Mời trên UI).</summary>
    [HttpGet("classes/{classId:guid}/available-students")]
    [ProducesResponseType(typeof(ApiResponse<List<AvailableStudentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableStudents(Guid classId, [FromQuery] string? search, CancellationToken ct)
    {
        var result = await _teamService.GetAvailableStudentsAsync(classId, search, ct);
        return Ok(ApiResponse<List<AvailableStudentResponse>>.Ok(result));
    }

    /// <summary>Lấy danh sách các nhóm trong 1 lớp học.</summary>
    [HttpGet("classes/{classId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<List<TeamResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeamsByClass(Guid classId, CancellationToken ct)
    {
        var result = await _teamService.GetTeamsByClassAsync(classId, ct);
        return Ok(ApiResponse<List<TeamResponse>>.Ok(result));
    }

    /// <summary>Xem chi tiết 1 nhóm kèm thành viên.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeamById(Guid id, CancellationToken ct)
    {
        var result = await _teamService.GetTeamByIdAsync(id, ct);
        return Ok(ApiResponse<TeamResponse>.Ok(result));
    }

    /// <summary>Nhóm trưởng gửi lời mời thành viên bằng Mã sinh viên.</summary>
    [HttpPost("{id:guid}/invitations")]
    [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> InviteMember(Guid id, [FromBody] InviteMemberRequest request, CancellationToken ct)
    {
        var result = await _teamService.InviteMemberAsync(id, request, ct);
        return Ok(ApiResponse<TeamResponse>.Ok(result, "Đã gửi lời mời tham gia nhóm"));
    }

    /// <summary>Sinh viên xem danh sách các lời mời vào nhóm đang chờ phản hồi của mình.</summary>
    [HttpGet("my-invitations")]
    [ProducesResponseType(typeof(ApiResponse<List<MyInvitationResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyInvitations(CancellationToken ct)
    {
        var result = await _teamService.GetMyInvitationsAsync(ct);
        return Ok(ApiResponse<List<MyInvitationResponse>>.Ok(result));
    }

    /// <summary>Sinh viên chấp nhận hoặc từ chối lời mời vào nhóm.</summary>
    [HttpPost("{id:guid}/respond-invitation")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RespondInvitation(Guid id, [FromBody] RespondInvitationRequest request, CancellationToken ct)
    {
        await _teamService.RespondInvitationAsync(id, request, ct);
        var msg = request.Accept ? "Đã tham gia nhóm thành công" : "Đã từ chối lời mời";
        return Ok(ApiResponse.Ok(msg));
    }
}
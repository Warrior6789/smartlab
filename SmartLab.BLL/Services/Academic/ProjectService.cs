using Microsoft.EntityFrameworkCore;

using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class ProjectService : IProjectService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ProjectService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<ProjectResponse> SubmitProjectAsync(SubmitProjectRequest request, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            throw new UnauthorizedException();

        // 1. Kiểm tra nhóm tồn tại và người gọi phải là Nhóm trưởng
        var team = await _uow.Repository<Team>().Query()
            .Include(t => t.Leader)
            .FirstOrDefaultAsync(t => t.TeamId == request.TeamId && t.ClassId == request.ClassId, ct)
            ?? throw new NotFoundException("Nhóm", request.TeamId);

        if (team.Leader.UserId != _currentUser.UserId.Value)
            throw new ForbiddenException("Chỉ nhóm trưởng mới có quyền đăng ký đề tài cho nhóm.");

        // 2. Mỗi nhóm chỉ được có tối đa 1 đề tài trong môn học
        var existingProject = await _uow.Repository<Project>().QueryNoTracking()
            .FirstOrDefaultAsync(p => p.TeamId == request.TeamId, ct);
        if (existingProject != null)
            throw new ConflictException("Nhóm của bạn đã đăng ký đề tài trong lớp này rồi.");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BadRequestException("Tên đề tài không được để trống.");

        if (request.Components == null || !request.Components.Any())
            throw new BadRequestException("Vui lòng đính kèm ít nhất 1 linh kiện cần mượn cho đề tài.");

        // 3. Kiểm tra các linh kiện có hợp lệ không
        var componentRepo = _uow.Repository<Component>();
        foreach (var c in request.Components)
        {
            if (c.Quantity <= 0)
                throw new BadRequestException("Số lượng linh kiện phải lớn hơn 0.");

            var exists = await componentRepo.QueryNoTracking()
                .AnyAsync(comp => comp.ComponentId == c.ComponentId && comp.IsActive, ct);
            if (!exists)
                throw new NotFoundException($"Không tìm thấy linh kiện hoặc linh kiện đã ngưng hoạt động: {c.ComponentId}");
        }

        var now = DateTime.UtcNow;
        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            OwnerId = _currentUser.UserId.Value,
            ClassId = request.ClassId,
            TeamId = request.TeamId,
            Status = "PendingApproval", // Chờ giảng viên duyệt
            CreatedAt = now,
            UpdatedAt = now,
            SubmittedAt = now
        };

        foreach (var c in request.Components)
        {
            project.ProjectComponents.Add(new ProjectComponent
            {
                ProjectId = project.ProjectId,
                ComponentId = c.ComponentId,
                Quantity = c.Quantity,
                Note = string.IsNullOrWhiteSpace(c.Note) ? null : c.Note.Trim()
            });
        }

        await _uow.Repository<Project>().AddAsync(project, ct);
        await _uow.SaveChangesAsync(ct);

        return await GetProjectByIdAsync(project.ProjectId, ct);
    }

    public async Task<ProjectResponse> ReviewProjectAsync(Guid projectId, ReviewProjectRequest request, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
            throw new UnauthorizedException();

        var project = await _uow.Repository<Project>().Query()
            .Include(p => p.Class).ThenInclude(c => c!.Instructor)
            .Include(p => p.ProjectComponents)
            .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
            ?? throw new NotFoundException("Đề tài", projectId);

        // Kiểm tra quyền: Phải là Admin hoặc Giảng viên phụ trách lớp này
        var isAdmin = _currentUser.IsInRole(RoleNames.Admin);
        var isInstructorOfClass = false;

        if (_currentUser.IsInRole(RoleNames.Lecturer) && project.Class?.Instructor != null)
        {
            isInstructorOfClass = project.Class.Instructor.UserId == _currentUser.UserId.Value;
        }

        if (!isAdmin && !isInstructorOfClass)
            throw new ForbiddenException("Bạn không có quyền phê duyệt đề tài của lớp học này.");

        var now = DateTime.UtcNow;
        project.Status = request.Approve ? "Approved" : "Rejected";
        project.ReviewedBy = _currentUser.UserId.Value;
        project.ReviewedAt = now;
        project.ReviewComment = string.IsNullOrWhiteSpace(request.ReviewComment) ? null : request.ReviewComment.Trim();
        project.UpdatedAt = now;

        // Nếu giảng viên có điều chỉnh lại số lượng linh kiện được mượn
        if (request.AdjustedComponents != null && request.AdjustedComponents.Any())
        {
            project.ProjectComponents.Clear();
            foreach (var item in request.AdjustedComponents)
            {
                if (item.Quantity > 0)
                {
                    project.ProjectComponents.Add(new ProjectComponent
                    {
                        ProjectId = project.ProjectId,
                        ComponentId = item.ComponentId,
                        Quantity = item.Quantity,
                        Note = item.Note
                    });
                }
            }
        }

        await _uow.SaveChangesAsync(ct);
        return await GetProjectByIdAsync(projectId, ct);
    }

    public async Task<List<ProjectResponse>> GetProjectsByClassAsync(Guid classId, CancellationToken ct = default)
    {
        var projects = await _uow.Repository<Project>().QueryNoTracking()
            .Include(p => p.Class)
            .Include(p => p.Team)
            .Include(p => p.ReviewedByNavigation)
            .Include(p => p.ProjectComponents).ThenInclude(pc => pc.Component).ThenInclude(c => c.Category)
            .Where(p => p.ClassId == classId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return projects.Select(MapToResponse).ToList();
    }

    public async Task<ProjectResponse> GetProjectByIdAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _uow.Repository<Project>().QueryNoTracking()
            .Include(p => p.Class)
            .Include(p => p.Team)
            .Include(p => p.ReviewedByNavigation)
            .Include(p => p.ProjectComponents).ThenInclude(pc => pc.Component).ThenInclude(c => c.Category)
            .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
            ?? throw new NotFoundException("Đề tài", projectId);

        return MapToResponse(project);
    }

    public async Task<ProjectResponse?> GetProjectByTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var project = await _uow.Repository<Project>().QueryNoTracking()
            .Include(p => p.Class)
            .Include(p => p.Team)
            .Include(p => p.ReviewedByNavigation)
            .Include(p => p.ProjectComponents).ThenInclude(pc => pc.Component).ThenInclude(c => c.Category)
            .FirstOrDefaultAsync(p => p.TeamId == teamId, ct);

        return project == null ? null : MapToResponse(project);
    }

    private static ProjectResponse MapToResponse(Project project)
    {
        return new ProjectResponse
        {
            ProjectId = project.ProjectId,
            ClassId = project.ClassId ?? Guid.Empty,
            ClassCode = project.Class?.ClassCode ?? string.Empty,
            TeamId = project.TeamId ?? Guid.Empty,
            TeamName = project.Team?.TeamName ?? string.Empty,
            Title = project.Title,
            Description = project.Description,
            Status = project.Status,
            CreatedAt = project.CreatedAt,
            SubmittedAt = project.SubmittedAt,
            ReviewerName = project.ReviewedByNavigation?.FullName,
            ReviewedAt = project.ReviewedAt,
            ReviewComment = project.ReviewComment,
            Components = project.ProjectComponents.Select(pc => new ProjectComponentResponse
            {
                ComponentId = pc.ComponentId,
                ComponentCode = pc.Component.ComponentCode,
                ComponentName = pc.Component.ComponentName,
                CategoryName = pc.Component.Category?.CategoryName,
                Quantity = pc.Quantity,
                Note = pc.Note
            }).ToList()
        };
    }
}
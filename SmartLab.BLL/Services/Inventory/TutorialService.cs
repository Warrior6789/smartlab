using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.BLL.Mappings.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class TutorialService : ITutorialService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public TutorialService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<List<TutorialResponse>> GetByComponentAsync(Guid componentId, CancellationToken ct = default)
    {
        await EnsureComponentExistsAsync(componentId, ct);

        return await _uow.Repository<ComponentTutorial>().QueryNoTracking()
            .Where(t => t.ComponentId == componentId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TutorialResponse
            {
                TutorialId = t.TutorialId,
                ComponentId = t.ComponentId,
                Title = t.Title,
                Content = t.Content,
                VideoUrl = t.VideoUrl,
                AuthorId = t.AuthorId,
                AuthorName = t.Author.FullName,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<TutorialResponse> CreateAsync(Guid componentId, TutorialRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        await EnsureComponentExistsAsync(componentId, ct);

        var author = await _uow.Repository<User>().Query()
            .FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new UnauthorizedException();

        var now = DateTime.UtcNow;
        var tutorial = new ComponentTutorial
        {
            ComponentId = componentId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            VideoUrl = NormalizeVideoUrl(request.VideoUrl),
            Author = author,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _uow.Repository<ComponentTutorial>().AddAsync(tutorial, ct);
        await _uow.SaveChangesAsync(ct);

        return TutorialMappings.ToTutorialResponse(tutorial);
    }

    public async Task<TutorialResponse> UpdateAsync(
        Guid componentId, Guid tutorialId, TutorialRequest request, CancellationToken ct = default)
    {
        var tutorial = await GetOwnedTutorialAsync(componentId, tutorialId, ct);

        tutorial.Title = request.Title.Trim();
        tutorial.Content = request.Content.Trim();
        tutorial.VideoUrl = NormalizeVideoUrl(request.VideoUrl);
        tutorial.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync(ct);

        return TutorialMappings.ToTutorialResponse(tutorial);
    }

    public async Task DeleteAsync(Guid componentId, Guid tutorialId, CancellationToken ct = default)
    {
        var tutorial = await GetOwnedTutorialAsync(componentId, tutorialId, ct);

        _uow.Repository<ComponentTutorial>().Remove(tutorial);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>Loads the tutorial (with Author) under the given component and checks the caller is its author or an Admin.</summary>
    private async Task<ComponentTutorial> GetOwnedTutorialAsync(Guid componentId, Guid tutorialId, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var tutorial = await _uow.Repository<ComponentTutorial>().Query()
            .Include(t => t.Author)
            .FirstOrDefaultAsync(t => t.TutorialId == tutorialId && t.ComponentId == componentId, ct)
            ?? throw new NotFoundException("Không tìm thấy bài hướng dẫn");

        if (tutorial.AuthorId != userId && !_currentUser.IsInRole(RoleNames.Admin))
            throw new ForbiddenException("Bạn chỉ được sửa hoặc xóa bài hướng dẫn do mình viết");

        return tutorial;
    }

    private async Task EnsureComponentExistsAsync(Guid componentId, CancellationToken ct)
    {
        if (!await _uow.Repository<Component>().QueryNoTracking().AnyAsync(c => c.ComponentId == componentId, ct))
            throw new NotFoundException("Không tìm thấy linh kiện");
    }

    private static string? NormalizeVideoUrl(string? videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
            return null;

        return videoUrl.Trim();
    }
}

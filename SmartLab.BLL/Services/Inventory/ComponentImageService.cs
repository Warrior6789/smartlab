using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.External.Storage;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.BLL.Mappings.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class ComponentImageService : IComponentImageService
{
    private const string StorageFolder = "components";

    private readonly IUnitOfWork _uow;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ComponentImageService> _logger;

    public ComponentImageService(
        IUnitOfWork uow, IFileStorage fileStorage, ICurrentUserService currentUser, ILogger<ComponentImageService> logger)
    {
        _uow = uow;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ComponentImageResponse> UploadImageAsync(
        Guid componentId, UploadComponentImageRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        await EnsureComponentExistsAsync(componentId, ct);

        var images = _uow.Repository<ComponentImage>();
        var hasPrimary = await images.QueryNoTracking().AnyAsync(i => i.ComponentId == componentId && i.IsPrimary, ct);

        StoredFile stored;
        await using (var content = request.File.OpenReadStream())
        {
            stored = await _fileStorage.UploadImageAsync(content, request.File.FileName, StorageFolder, ct);
        }

        var image = new ComponentImage
        {
            ComponentId = componentId,
            ImageUrl = stored.Url,
            StorageKey = stored.Key,
            ImageType = request.ImageType,
            IsPrimary = !hasPrimary,
            UploadedBy = userId,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            await images.AddAsync(image, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            await TryDeleteStoredFileAsync(stored.Key);
            throw;
        }

        return ComponentMappings.ToImageResponse(image);
    }

    public async Task DeleteImageAsync(Guid componentId, Guid imageId, CancellationToken ct = default)
    {
        var imageRepo = _uow.Repository<ComponentImage>();
        var images = await imageRepo.Query()
            .Where(i => i.ComponentId == componentId)
            .OrderBy(i => i.CreatedAt).ThenBy(i => i.ImageId)
            .ToListAsync(ct);

        var target = images.FirstOrDefault(i => i.ImageId == imageId)
                     ?? throw new NotFoundException("Không tìm thấy ảnh");

        imageRepo.Remove(target);
        if (target.IsPrimary)
        {
            var next = images.FirstOrDefault(i => i.ImageId != imageId);
            if (next != null)
                next.IsPrimary = true;
        }

        await _uow.SaveChangesAsync(ct);

        if (target.StorageKey != null)
            await TryDeleteStoredFileAsync(target.StorageKey);
    }

    private async Task EnsureComponentExistsAsync(Guid componentId, CancellationToken ct)
    {
        if (!await _uow.Repository<Component>().QueryNoTracking().AnyAsync(c => c.ComponentId == componentId, ct))
            throw new NotFoundException("Không tìm thấy linh kiện");
    }

    /// <summary>The DB row is the source of truth; a leftover file in storage is only logged, never fails the request.</summary>
    private async Task TryDeleteStoredFileAsync(string key)
    {
        try
        {
            await _fileStorage.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete stored file {StorageKey}", key);
        }
    }
}

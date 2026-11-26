using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface IComponentImageService
{
    /// <summary>Uploads to file storage and saves the URL + storage key. The first image becomes primary.</summary>
    Task<ComponentImageResponse> UploadImageAsync(Guid componentId, UploadComponentImageRequest request, CancellationToken ct = default);

    /// <summary>Deletes the image row and its stored file; if it was primary, the oldest remaining image becomes primary.</summary>
    Task DeleteImageAsync(Guid componentId, Guid imageId, CancellationToken ct = default);
}

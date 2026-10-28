using System.Net;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SmartLab.BLL.Common.Exceptions;

namespace SmartLab.BLL.External.Storage;

/// <summary>
/// Cloudinary-backed <see cref="IFileStorage"/>. The app starts without Cloudinary settings;
/// only an actual upload/delete fails, so teammates not working on images need no extra secrets.
/// </summary>
public class CloudinaryFileStorage : IFileStorage
{
    private readonly CloudinaryOptions _options;
    private readonly Lazy<Cloudinary> _cloudinary;

    public CloudinaryFileStorage(IOptions<CloudinaryOptions> options)
    {
        _options = options.Value;
        _cloudinary = new Lazy<Cloudinary>(CreateClient);
    }

    public Task<StoredFile> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken ct = default)
        => UploadAsync(content, fileName, folder, ct);

    public Task<StoredFile> UploadPdfAsync(Stream content, string fileName, string folder, CancellationToken ct = default)
        => UploadAsync(content, fileName, folder, ct);

    public string GetPdfPreviewUrl(string key)
        => $"https://res.cloudinary.com/{_options.CloudName}/image/upload/pg_1,w_600/{key}.jpg";

    private async Task<StoredFile> UploadAsync(Stream content, string fileName, string folder, CancellationToken ct)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, content),
            Folder = $"{_options.RootFolder}/{folder}",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false,
        };

        var result = await _cloudinary.Value.UploadAsync(uploadParams, ct);
        if (result.Error != null && result.StatusCode == HttpStatusCode.BadRequest)
            throw new BadRequestException("File bị hỏng hoặc không đúng định dạng");
        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return new StoredFile
        {
            Key = result.PublicId,
            Url = result.SecureUrl.ToString(),
        };
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => DeleteAsync(key, ResourceType.Image);

    /// <summary>Stored as a raw resource; the public id keeps the .zip extension so the URL downloads as a ZIP.</summary>
    public async Task<StoredFile> UploadZipAsync(Stream content, string fileName, string folder, CancellationToken ct = default)
    {
        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(fileName, content),
            Folder = $"{_options.RootFolder}/{folder}",
            PublicId = $"{Guid.NewGuid():N}.zip",
            Overwrite = false,
        };

        var result = await _cloudinary.Value.UploadAsync(uploadParams, "raw", ct);
        if (result.Error != null && result.StatusCode == HttpStatusCode.BadRequest)
            throw new BadRequestException("File bị hỏng hoặc không đúng định dạng");
        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return new StoredFile
        {
            Key = result.PublicId,
            Url = result.SecureUrl.ToString(),
        };
    }

    public Task DeleteZipAsync(string key, CancellationToken ct = default)
        => DeleteAsync(key, ResourceType.Raw);

    private async Task DeleteAsync(string key, ResourceType resourceType)
    {
        var result = await _cloudinary.Value.DestroyAsync(new DeletionParams(key) { ResourceType = resourceType, Invalidate = true });
        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary delete failed: {result.Error.Message}");
    }

    private Cloudinary CreateClient()
    {
        if (!_options.IsConfigured)
            throw new InvalidOperationException(
                "Cloudinary is not configured. Set Cloudinary:CloudName, Cloudinary:ApiKey and Cloudinary:ApiSecret with dotnet user-secrets -p SmartLab.API");

        return new Cloudinary(new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret)) { Api = { Secure = true } };
    }
}

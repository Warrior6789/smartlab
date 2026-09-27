using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

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

    public async Task<StoredFile> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken ct = default)
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
        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return new StoredFile
        {
            Key = result.PublicId,
            Url = result.SecureUrl.ToString(),
        };
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var result = await _cloudinary.Value.DestroyAsync(new DeletionParams(key) { ResourceType = ResourceType.Image });
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

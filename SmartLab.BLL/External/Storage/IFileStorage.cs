namespace SmartLab.BLL.External.Storage;

/// <summary>Stores uploaded files outside the database. Services keep only the returned URL/key.</summary>
public interface IFileStorage
{
    /// <summary>Uploads an image under <paramref name="folder"/> (e.g. "components") with a unique name.</summary>
    Task<StoredFile> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken ct = default);

    /// <summary>Deletes a file by the <see cref="StoredFile.Key"/> returned from upload. Missing files are ignored.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);
}

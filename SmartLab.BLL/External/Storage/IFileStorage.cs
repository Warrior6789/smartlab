namespace SmartLab.BLL.External.Storage;

/// <summary>Stores uploaded files outside the database. Services keep only the returned URL/key.</summary>
public interface IFileStorage
{
    /// <summary>Uploads an image under <paramref name="folder"/> (e.g. "components") with a unique name.</summary>
    Task<StoredFile> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken ct = default);

    /// <summary>
    /// Uploads a PDF under <paramref name="folder"/> with a unique name. It is stored as an image resource,
    /// so the returned URL opens the PDF and <see cref="GetPdfPreviewUrl"/> can render a page as an image.
    /// </summary>
    Task<StoredFile> UploadPdfAsync(Stream content, string fileName, string folder, CancellationToken ct = default);

    /// <summary>URL of a JPG image of the first page of a PDF uploaded with <see cref="UploadPdfAsync"/>.</summary>
    string GetPdfPreviewUrl(string key);

    /// <summary>Deletes a file by the <see cref="StoredFile.Key"/> returned from upload. Missing files are ignored.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Uploads a ZIP under <paramref name="folder"/> with a unique name; the URL downloads the file.</summary>
    Task<StoredFile> UploadZipAsync(Stream content, string fileName, string folder, CancellationToken ct = default);

    /// <summary>Deletes a ZIP by the <see cref="StoredFile.Key"/> returned from <see cref="UploadZipAsync"/>. Missing files are ignored.</summary>
    Task DeleteZipAsync(string key, CancellationToken ct = default);
}

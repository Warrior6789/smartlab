namespace SmartLab.BLL.External.Storage;

/// <summary>
/// CloudName/ApiKey/ApiSecret only in user-secrets (dev) or environment variables Cloudinary__* (deploy). Never commit them.
/// </summary>
public class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Top-level folder in Cloudinary, so dev and prod uploads can be kept apart.</summary>
    public string RootFolder { get; set; } = "smartlab";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudName) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}

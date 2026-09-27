namespace SmartLab.BLL.External.Storage;

public class StoredFile
{
    /// <summary>Provider id of the file, needed to delete it later.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Public HTTPS URL to show the file.</summary>
    public string Url { get; set; } = string.Empty;
}

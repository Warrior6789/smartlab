namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on storage_cabinets.status.</summary>
public static class CabinetStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static readonly IReadOnlyList<string> All = new[] { Active, Inactive };

    /// <summary>"active" / " ACTIVE " → "Active"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}

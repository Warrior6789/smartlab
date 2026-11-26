namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on component_items.status.</summary>
public static class ComponentItemStatuses
{
    public const string Available = "Available";
    public const string Borrowed = "Borrowed";
    public const string Lost = "Lost";
    public const string Retired = "Retired";

    public static readonly IReadOnlyList<string> All = new[] { Available, Borrowed, Lost, Retired };

    /// <summary>"available" / " AVAILABLE " → "Available"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}

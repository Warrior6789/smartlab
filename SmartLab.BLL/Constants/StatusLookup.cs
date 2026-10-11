namespace SmartLab.BLL.Constants;

/// <summary>Case-insensitive lookup of a status sent in a query string ("available", " ACTIVE ").</summary>
internal static class StatusLookup
{
    /// <summary>The matching value from <paramref name="all"/> in its stored spelling; null when there is none.</summary>
    public static string? Find(IReadOnlyList<string> all, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();
        return all.FirstOrDefault(s => s.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }
}

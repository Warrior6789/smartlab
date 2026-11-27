namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on components.tracking_type.</summary>
public static class ComponentTrackingTypes
{
    public const string Consumable = "CONSUMABLE";
    public const string Reusable = "REUSABLE";
    public const string HighValue = "HIGH_VALUE";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Consumable, Reusable, HighValue };

    /// <summary>Vietnamese name shown to users (Tiêu hao, Tái sử dụng, Giá trị cao); an unknown code comes back as is.</summary>
    public static string Label(string? trackingType)
    {
        if (trackingType == Consumable)
            return "Tiêu hao";
        if (trackingType == Reusable)
            return "Tái sử dụng";
        if (trackingType == HighValue)
            return "Giá trị cao";
        return trackingType ?? "";
    }
}

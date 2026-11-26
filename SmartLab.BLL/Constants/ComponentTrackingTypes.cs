namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on components.tracking_type.</summary>
public static class ComponentTrackingTypes
{
    public const string Consumable = "CONSUMABLE";
    public const string Reusable = "REUSABLE";
    public const string HighValue = "HIGH_VALUE";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Consumable, Reusable, HighValue };
}

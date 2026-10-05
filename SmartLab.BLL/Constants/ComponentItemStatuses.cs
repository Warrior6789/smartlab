namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on component_items.status.</summary>
public static class ComponentItemStatuses
{
    public const string Available = "Available";
    public const string Borrowed = "Borrowed";
    public const string Broken = "Broken";
    public const string Lost = "Lost";
    public const string Maintenance = "Maintenance";
    public const string Retired = "Retired";
}

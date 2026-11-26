namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on component_images.image_type.</summary>
public static class ComponentImageTypes
{
    public const string Product = "Product";
    public const string Pinout = "Pinout";

    public static readonly IReadOnlyList<string> All = new[] { Product, Pinout };
}

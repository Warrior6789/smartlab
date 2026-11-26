namespace SmartLab.BLL.DTOs.Inventory;

public class ComponentImageResponse
{
    public Guid ImageId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
}

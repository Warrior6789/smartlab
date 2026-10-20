using SmartLab.BLL.DTOs.Inventory;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Inventory;

public static class ComponentMappings
{
    public static ComponentImageResponse ToImageResponse(ComponentImage image) => new()
    {
        ImageId = image.ImageId,
        ImageUrl = image.ImageUrl,
        ImageType = image.ImageType,
        IsPrimary = image.IsPrimary,
        CreatedAt = image.CreatedAt,
    };
}

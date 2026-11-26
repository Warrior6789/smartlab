using SmartLab.BLL.DTOs.Inventory;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Inventory;

public static class TutorialMappings
{
    /// <summary>Requires Author to be loaded.</summary>
    public static TutorialResponse ToTutorialResponse(ComponentTutorial tutorial) => new()
    {
        TutorialId = tutorial.TutorialId,
        ComponentId = tutorial.ComponentId,
        Title = tutorial.Title,
        Content = tutorial.Content,
        VideoUrl = tutorial.VideoUrl,
        AuthorId = tutorial.AuthorId,
        AuthorName = tutorial.Author.FullName,
        CreatedAt = tutorial.CreatedAt,
        UpdatedAt = tutorial.UpdatedAt,
    };
}

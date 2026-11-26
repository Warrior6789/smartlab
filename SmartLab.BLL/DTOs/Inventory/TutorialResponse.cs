namespace SmartLab.BLL.DTOs.Inventory;

public class TutorialResponse
{
    public Guid TutorialId { get; set; }
    public Guid ComponentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

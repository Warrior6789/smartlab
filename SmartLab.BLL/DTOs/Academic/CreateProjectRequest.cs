namespace SmartLab.BLL.DTOs.Academic;

public class CreateProjectRequest
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional while drafting; required before the project is sent for review.</summary>
    public string? Description { get; set; }

    /// <summary>Components the project needs (may be empty while drafting).</summary>
    public List<ProjectComponentRequest> Components { get; set; } = new();

    /// <summary>Items the team buys itself (not in the lab catalog); may be empty.</summary>
    public List<SelfSuppliedItemRequest> SelfSuppliedItems { get; set; } = new();
}

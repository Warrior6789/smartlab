namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// Replaces title, description, the whole component list and the whole self-supplied list
/// (send the full form, not only the changed fields).
/// </summary>
public class UpdateProjectRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Required (may be empty); the saved list is replaced by this one.</summary>
    public List<ProjectComponentRequest>? Components { get; set; }

    /// <summary>Required (may be empty); the saved self-supplied list is replaced by this one.</summary>
    public List<SelfSuppliedItemRequest>? SelfSuppliedItems { get; set; }
}

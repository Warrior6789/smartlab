namespace SmartLab.BLL.DTOs.Academic;

/// <summary>What to preview: the given components (a supplementary borrow being written), or the saved project components when empty.</summary>
public class BorrowPreviewRequest
{
    public List<ProjectComponentRequest> Components { get; set; } = new();
}

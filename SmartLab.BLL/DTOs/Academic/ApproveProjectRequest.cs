namespace SmartLab.BLL.DTOs.Academic;

public class ApproveProjectRequest
{
    /// <summary>Optional note for the team; required when quantities are adjusted, so the team knows why.</summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Optional: approve with lower quantities (only lower, 0 removes a component). Components not listed keep the
    /// quantity the team asked for. At least one component must stay; otherwise reject the project instead.
    /// </summary>
    public List<ApproveQuantityAdjustmentRequest> Adjustments { get; set; } = new();

    /// <summary>
    /// The lab request to decide: a project borrows from each lab in its own request, decided one by one. Required when the
    /// project has several pending requests; may be left out when it has one.
    /// </summary>
    public Guid? BorrowRequestId { get; set; }
}

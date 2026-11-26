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
}

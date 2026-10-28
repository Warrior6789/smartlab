namespace SmartLab.BLL.DTOs.Academic;

/// <summary>A final submission waiting for the lecturer of the project's class.</summary>
public class PendingFinalSubmissionResponse
{
    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? TeamName { get; set; }

    public string? ClassCode { get; set; }

    public string? LeaderName { get; set; }

    public string? GithubUrl { get; set; }

    public string? SlideUrl { get; set; }

    public string? DemoVideoUrl { get; set; }

    public string? ReportUrl { get; set; }

    public string? SourceCodeUrl { get; set; }

    public DateTime? FinalSubmittedAt { get; set; }
}

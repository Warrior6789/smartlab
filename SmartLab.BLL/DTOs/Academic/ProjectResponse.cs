namespace SmartLab.BLL.DTOs.Academic;

public class ProjectResponse
{
    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? TeamId { get; set; }

    public string? TeamName { get; set; }

    public Guid? ClassId { get; set; }

    public string? ClassCode { get; set; }

    public Guid OwnerId { get; set; }

    public string OwnerName { get; set; } = string.Empty;

    public int ComponentCount { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>Comment of the latest decision (the reject reason when Rejected); cleared when the project is sent again.</summary>
    public string? ReviewComment { get; set; }

    /// <summary>Final submission: null (not submitted), Pending, Accepted or Returned.</summary>
    public string? FinalStatus { get; set; }

    public DateTime? FinalSubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

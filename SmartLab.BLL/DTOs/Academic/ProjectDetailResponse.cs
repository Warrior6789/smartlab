using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.DTOs.Academic;

public class ProjectDetailResponse : ProjectResponse
{
    public string? Description { get; set; }

    public string? GithubUrl { get; set; }

    public string? SlideUrl { get; set; }

    public string? DemoVideoUrl { get; set; }

    public Guid? ReviewedBy { get; set; }

    public string? ReviewedByName { get; set; }

    public Guid? FinalReviewedBy { get; set; }

    public string? FinalReviewedByName { get; set; }

    public DateTime? FinalReviewedAt { get; set; }

    /// <summary>Lecturer's comment on the final submission (required when Returned); cleared when the team submits again.</summary>
    public string? FinalComment { get; set; }

    public List<ProjectComponentResponse> Components { get; set; } = new();

    /// <summary>Items the team buys itself, not borrowed from the lab.</summary>
    public List<SelfSuppliedItemResponse> SelfSuppliedItems { get; set; } = new();

    /// <summary>Borrow requests created by each submit, newest first.</summary>
    public List<BorrowRequestSummaryResponse> BorrowRequests { get; set; } = new();
}

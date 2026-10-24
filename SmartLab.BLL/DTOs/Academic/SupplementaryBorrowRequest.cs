namespace SmartLab.BLL.DTOs.Academic;

public class SupplementaryBorrowRequest
{
    /// <summary>Why the team needs more components; the lecturer reads it before approving.</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime? BorrowDate { get; set; }

    public DateTime? DueDate { get; set; }

    /// <summary>Components to borrow in addition to the approved list.</summary>
    public List<ProjectComponentRequest> Components { get; set; } = new();
}

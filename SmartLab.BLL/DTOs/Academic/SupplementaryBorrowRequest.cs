namespace SmartLab.BLL.DTOs.Academic;

public class SupplementaryBorrowRequest
{
    /// <summary>Why the team needs more components; the lecturer reads it before approving.</summary>
    public string Reason { get; set; } = string.Empty;

    [Obsolete("Ignored: the due date comes from the semester of the class.")]
    public DateTime? BorrowDate { get; set; }

    [Obsolete("Ignored: the due date comes from the semester of the class.")]
    public DateTime? DueDate { get; set; }

    /// <summary>Components to borrow in addition to the approved list.</summary>
    public List<ProjectComponentRequest> Components { get; set; } = new();
}

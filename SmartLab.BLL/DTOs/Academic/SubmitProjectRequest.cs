namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// Sends the project for review and creates the borrow request for its lab components, in the lab of the class's
/// course. The team may choose the pickup day; the due date always comes from the semester of the class
/// (see GET /api/projects/{id}/borrow-deadline).
/// </summary>
public class SubmitProjectRequest
{
    /// <summary>
    /// Day to pick the components up (Vietnam date, e.g. "2026-11-25"): from today to the due date. Null = today.
    /// </summary>
    public DateTime? BorrowDate { get; set; }

    /// <summary>Ignored by the backend; kept so older clients that still send it are not rejected.</summary>
    [Obsolete("Ignored: the due date comes from the semester of the class.")]
    public DateTime? DueDate { get; set; }
}

namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// Sends the project for review and creates the borrow request for its lab components. The system picks the pickup lab
/// and students enter no dates: the borrow date is today and the due date comes from the semester of the class
/// (see GET /api/projects/{id}/borrow-deadline). Send an empty JSON object <c>{}</c>.
/// </summary>
public class SubmitProjectRequest
{
    /// <summary>Ignored by the backend; kept so older clients that still send it are not rejected.</summary>
    [Obsolete("Ignored: the borrow date is the day the project is submitted.")]
    public DateTime? BorrowDate { get; set; }

    /// <summary>Ignored by the backend; kept so older clients that still send it are not rejected.</summary>
    [Obsolete("Ignored: the due date comes from the semester of the class.")]
    public DateTime? DueDate { get; set; }
}

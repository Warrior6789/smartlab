namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// Sends the project for review and creates the borrow request for its components. Students always pick up at the
/// common pickup lab, so no lab is chosen here.
/// </summary>
public class SubmitProjectRequest
{
    /// <summary>Vietnam calendar date (yyyy-MM-dd); today or later.</summary>
    public DateTime? BorrowDate { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd); after BorrowDate.</summary>
    public DateTime? DueDate { get; set; }
}

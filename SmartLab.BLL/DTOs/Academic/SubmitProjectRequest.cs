namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// Sends the project for review and creates the borrow request for its lab components. Students always pick up at the
/// common pickup lab, so no lab is chosen here. A project that only uses self-supplied items creates no borrow request,
/// so the dates may then be left out.
/// </summary>
public class SubmitProjectRequest
{
    /// <summary>Vietnam calendar date (yyyy-MM-dd); today or later. Required when the project has lab components.</summary>
    public DateTime? BorrowDate { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd); after BorrowDate. Required when the project has lab components.</summary>
    public DateTime? DueDate { get; set; }
}

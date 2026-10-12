namespace SmartLab.BLL.DTOs.Academic;

/// <summary>Sends the project for review and creates the borrow request for its components.</summary>
public class SubmitProjectRequest
{
    /// <summary>Lab the components are borrowed from; it must hold enough of each.</summary>
    public Guid RoomId { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd); today or later.</summary>
    public DateTime? BorrowDate { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd); after BorrowDate.</summary>
    public DateTime? DueDate { get; set; }
}

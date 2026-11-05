namespace SmartLab.BLL.DTOs.Academic;

/// <summary>When components borrowed for a project come back: set by the semester of its class.</summary>
public class BorrowDeadlineResponse
{
    /// <summary>Start (00:00 Vietnam time) of the last day to return.</summary>
    public DateTime DueDate { get; set; }

    public string SemesterCode { get; set; } = string.Empty;

    /// <summary>Days from today to the last day to return; negative once it has passed.</summary>
    public int DaysLeft { get; set; }

    public bool CanBorrow { get; set; }
}

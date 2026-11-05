namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>What the AI read on a student card, to fill the search box of the queue.</summary>
public class StudentCardReadResponse
{
    /// <summary>False when the AI could not be reached, so the staff type the code themselves.</summary>
    public bool AiAvailable { get; set; }

    public bool IsStudentCard { get; set; }

    public string? FullName { get; set; }

    public string? StudentCode { get; set; }
}

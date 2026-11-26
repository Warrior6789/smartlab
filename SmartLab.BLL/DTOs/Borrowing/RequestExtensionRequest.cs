namespace SmartLab.BLL.DTOs.Borrowing;

public class RequestExtensionRequest
{
    /// <summary>Not used: extensions are turned off because components are borrowed for the semester of the class.</summary>
    public DateTime? NewDueDate { get; set; }

    public string Reason { get; set; } = string.Empty;
}

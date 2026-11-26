namespace SmartLab.BLL.DTOs.Borrowing;

public class RejectExtensionRequest
{
    /// <summary>Why the extension is refused; it is emailed to the student.</summary>
    public string Reason { get; set; } = string.Empty;
}

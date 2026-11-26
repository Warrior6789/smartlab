namespace SmartLab.BLL.DTOs.Academic;

public class RejectFinalSubmissionRequest
{
    /// <summary>Required: the team reads it to fix the work before submitting again.</summary>
    public string Comment { get; set; } = string.Empty;
}

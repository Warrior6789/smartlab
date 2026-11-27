namespace SmartLab.BLL.DTOs.Academic;

public class RejectProjectRequest
{
    /// <summary>Required: the team reads it to fix the project before sending it again.</summary>
    public string Reason { get; set; } = string.Empty;
}

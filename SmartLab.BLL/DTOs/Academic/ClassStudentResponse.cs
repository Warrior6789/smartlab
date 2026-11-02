namespace SmartLab.BLL.DTOs.Academic;

public class ClassStudentResponse
{
    public Guid StudentProfileId { get; set; }
    public Guid UserId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }

    /// <summary>Team the student is an Accepted member of in this class; null when there is none.</summary>
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public bool IsLeader { get; set; }
}
namespace SmartLab.BLL.DTOs.ClassSetupDTO;

/// <summary>One student of a class roster, with the team they belong to in that class (if any).</summary>
public class ClassStudentResponse
{
    public Guid StudentProfileId { get; set; }
    public Guid UserId { get; set; }
    public string StudentCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string? Major { get; set; }
    public string? Cohort { get; set; }

    /// <summary>Studying or Dropped (ClassStudentStatuses).</summary>
    public string Status { get; set; } = null!;
    public DateTime EnrolledAt { get; set; }

    /// <summary>Team the student has accepted in this class; null when they have none yet.</summary>
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public bool IsTeamLeader { get; set; }
}

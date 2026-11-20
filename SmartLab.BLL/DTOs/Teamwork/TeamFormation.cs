namespace SmartLab.BLL.DTOs.Teamwork;

/// <summary>Lecturer sets the team size of the class. Deadline only blocks student actions; it does not lock the class.</summary>
public class UpdateTeamFormationSettingsRequest
{
    public int MinTeamSize { get; set; }
    public int MaxTeamSize { get; set; }
    public DateTime? Deadline { get; set; }
}

public class TeamFormationResponse
{
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? MinTeamSize { get; set; }
    public int? MaxTeamSize { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? Deadline { get; set; }
    /// <summary>Open and the deadline has not passed: students may create, invite, accept and leave.</summary>
    public bool StudentsCanChangeTeams { get; set; }

    public int StudyingStudents { get; set; }
    public int TeamCount { get; set; }
    public int UnassignedCount { get; set; }
    /// <summary>Teams with fewer Accepted members than MinTeamSize.</summary>
    public int UnderMinTeamCount { get; set; }

    /// <summary>Filled for the class lecturer and Admin only.</summary>
    public IReadOnlyList<TeamFormationTeamItem> Teams { get; set; } = Array.Empty<TeamFormationTeamItem>();

    /// <summary>Studying students without an Accepted team. Filled for the class lecturer and Admin only.</summary>
    public IReadOnlyList<TeamFormationStudentItem> UnassignedStudents { get; set; } = Array.Empty<TeamFormationStudentItem>();
}

public class TeamFormationTeamItem
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public Guid LeaderId { get; set; }
    public string LeaderFullName { get; set; } = string.Empty;
    public int AcceptedCount { get; set; }
    public int PendingCount { get; set; }
    public bool HasProject { get; set; }
    public bool IsUnderMin { get; set; }
    public bool IsFull { get; set; }
    public IReadOnlyList<TeamFormationStudentItem> Members { get; set; } = Array.Empty<TeamFormationStudentItem>();
}

public class TeamFormationStudentItem
{
    public Guid StudentProfileId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

/// <summary>
/// Lecturer places students without a team, at any time: into existing teams without a project and/or into new teams.
/// Used for manual placement and to apply the auto-assign preview after the lecturer reviewed it.
/// </summary>
public class ApplyTeamAssignmentsRequest
{
    public List<ExistingTeamAssignment> ExistingTeams { get; set; } = new();
    public List<NewTeamAssignment> NewTeams { get; set; } = new();
    /// <summary>The lecturer confirmed that some teams end up with more members than MaxTeamSize.</summary>
    public bool AllowExceedMax { get; set; }
}

public class ExistingTeamAssignment
{
    public Guid TeamId { get; set; }
    public List<Guid> StudentProfileIds { get; set; } = new();
}

public class NewTeamAssignment
{
    public string TeamName { get; set; } = string.Empty;
    public Guid LeaderStudentProfileId { get; set; }
    /// <summary>Every member including the leader.</summary>
    public List<Guid> StudentProfileIds { get; set; } = new();
}

/// <summary>Lecturer moves a student who already has a team into another team; neither team may have a project.</summary>
public class MoveTeamMemberRequest
{
    public Guid StudentProfileId { get; set; }
    public Guid TargetTeamId { get; set; }
    public bool AllowExceedMax { get; set; }
}

/// <summary>Suggested placement; nothing is saved. Send it back through ApplyTeamAssignmentsRequest after review.</summary>
public class AutoAssignPreviewResponse
{
    public IReadOnlyList<AutoAssignExistingTeam> ExistingTeams { get; set; } = Array.Empty<AutoAssignExistingTeam>();
    public IReadOnlyList<AutoAssignNewTeam> NewTeams { get; set; } = Array.Empty<AutoAssignNewTeam>();
    /// <summary>True when applying the preview needs AllowExceedMax = true.</summary>
    public bool RequiresExceedMax { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public class AutoAssignExistingTeam
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int CurrentCount { get; set; }
    public IReadOnlyList<TeamFormationStudentItem> AddedStudents { get; set; } = Array.Empty<TeamFormationStudentItem>();
}

public class AutoAssignNewTeam
{
    public string TeamName { get; set; } = string.Empty;
    public Guid LeaderStudentProfileId { get; set; }
    public IReadOnlyList<TeamFormationStudentItem> Students { get; set; } = Array.Empty<TeamFormationStudentItem>();
}

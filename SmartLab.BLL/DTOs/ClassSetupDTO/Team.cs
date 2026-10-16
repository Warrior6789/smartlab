namespace SmartLab.BLL.DTOs.ClassSetupDTO;

public class CreateTeamRequest
{
    public Guid ClassId { get; set; }
    public string TeamName { get; set; } = null!;
    public List<string>? InvitedStudentCodes { get; set; }
}

public class InviteMemberRequest
{
    public string StudentCode { get; set; } = null!;
}

public class RespondInvitationRequest
{
    public bool Accept { get; set; }
}

public class AvailableStudentResponse
{
    public Guid StudentProfileId { get; set; }
    public string StudentCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
}

public class TeamResponse
{
    public Guid TeamId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = null!;
    public string TeamName { get; set; } = null!;
    public Guid LeaderId { get; set; }
    public string LeaderCode { get; set; } = null!;
    public string LeaderName { get; set; } = null!;
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TeamMemberItemResponse> Members { get; set; } = new();
}

public class TeamMemberItemResponse
{
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string RoleInTeam { get; set; } = null!; 
    public string Status { get; set; } = null!;     
    public DateTime JoinedOrInvitedAt { get; set; }
}

public class MyInvitationResponse
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = null!;
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = null!;
    public string LeaderName { get; set; } = null!;
    public DateTime InvitedAt { get; set; }
}
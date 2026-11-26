using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Teamwork;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Teamwork;

public static class TeamMappings
{
    public static TeamResponse ToResponse(Team team)
    {
        var accepted = team.TeamMembers.Count(tm => tm.Status == TeamMemberStatuses.Accepted);
        var pending = team.TeamMembers.Count(tm => tm.Status == TeamMemberStatuses.Pending);
        var cls = team.Class;
        var response = ToResponseCore(team);
        response.TeamFormationStatus = cls.TeamFormationStatus;
        response.MinTeamSize = cls.MinTeamSize;
        response.MaxTeamSize = cls.MaxTeamSize;
        response.MemberCount = accepted;
        response.IsFull = cls.MaxTeamSize is { } max && accepted + pending >= max;
        response.IsReadyForProject = pending == 0 && (cls.MinTeamSize is not { } min || accepted >= min);
        return response;
    }

    private static TeamResponse ToResponseCore(Team team) => new()
    {
        TeamId = team.TeamId,
        ClassId = team.ClassId,
        ClassCode = team.Class.ClassCode,
        TeamName = team.TeamName,
        LeaderId = team.LeaderId,
        CreatedAt = team.CreatedAt,
        UpdatedAt = team.UpdatedAt,
        Members = team.TeamMembers
            .OrderByDescending(tm => tm.StudentId == team.LeaderId)
            .ThenBy(tm => tm.Student.StudentCode)
            .Select(tm => new TeamMemberResponse
            {
                StudentProfileId = tm.StudentId,
                StudentCode = tm.Student.StudentCode,
                FullName = tm.Student.User.FullName,
                IsLeader = tm.StudentId == team.LeaderId,
                Status = tm.Status,
                InvitedAt = tm.InvitedAt,
                RespondedAt = tm.RespondedAt,
            })
            .ToList(),
    };

    public static TeamInvitationResponse ToInvitationResponse(TeamMember invitation) => new()
    {
        TeamId = invitation.TeamId,
        ClassId = invitation.Team.ClassId,
        ClassCode = invitation.Team.Class.ClassCode,
        TeamName = invitation.Team.TeamName,
        LeaderId = invitation.Team.LeaderId,
        LeaderStudentCode = invitation.Team.Leader.StudentCode,
        LeaderFullName = invitation.Team.Leader.User.FullName,
        Status = invitation.Status,
        InvitedAt = invitation.InvitedAt,
    };
}

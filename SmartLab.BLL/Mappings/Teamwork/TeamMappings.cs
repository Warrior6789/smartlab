using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Teamwork;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Teamwork;

public static class TeamMappings
{
    public static TeamResponse ToResponse(Team team) => new()
    {
        TeamId = team.TeamId,
        ClassId = team.ClassId,
        ClassCode = team.Class.ClassCode,
        TeamName = team.TeamName,
        LeaderId = team.LeaderId,
        IsReadyForProject = !team.TeamMembers.Any(tm => tm.Status == TeamMemberStatuses.Pending),
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

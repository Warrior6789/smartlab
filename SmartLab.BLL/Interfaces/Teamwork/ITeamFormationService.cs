using SmartLab.BLL.DTOs.Teamwork;

namespace SmartLab.BLL.Interfaces.Teamwork;

/// <summary>
/// Team formation of a class: the lecturer (or Admin) sets the team size and a deadline; students form teams themselves until
/// then. Afterwards the lecturer completes the teams (suggested automatically) and only the lecturer changes them.
/// </summary>
public interface ITeamFormationService
{
    /// <summary>Lecturer/Admin get teams and unassigned students; a student of the class gets the settings and counts only.</summary>
    Task<TeamFormationResponse> GetAsync(Guid classId, CancellationToken ct = default);

    Task<TeamFormationResponse> UpdateSettingsAsync(Guid classId, UpdateTeamFormationSettingsRequest request, CancellationToken ct = default);

    /// <summary>Ends the time students have to choose their team now; unanswered invitations are rejected.</summary>
    Task<TeamFormationResponse> CloseAsync(Guid classId, CancellationToken ct = default);

    /// <summary>The lecturer takes a student (not the leader) out of a team, e.g. one who stopped studying.</summary>
    Task<TeamFormationResponse> RemoveMemberAsync(Guid classId, Guid studentProfileId, CancellationToken ct = default);

    /// <summary>
    /// Suggested placement of the unassigned students and merging of teams below the minimum, only after the deadline; nothing is saved.
    /// </summary>
    Task<AutoAssignPreviewResponse> PreviewAutoAssignAsync(Guid classId, CancellationToken ct = default);

    Task<TeamFormationResponse> ApplyAssignmentsAsync(Guid classId, ApplyTeamAssignmentsRequest request, CancellationToken ct = default);

    Task<TeamFormationResponse> MoveMemberAsync(Guid classId, MoveTeamMemberRequest request, CancellationToken ct = default);
}

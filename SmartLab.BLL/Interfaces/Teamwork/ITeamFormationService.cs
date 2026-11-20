using SmartLab.BLL.DTOs.Teamwork;

namespace SmartLab.BLL.Interfaces.Teamwork;

/// <summary>
/// Team formation of a class: students form teams themselves (until the optional deadline); the lecturer (or Admin)
/// sets the size and deadline, and places or moves students at any time (into teams without a project).
/// </summary>
public interface ITeamFormationService
{
    /// <summary>Lecturer/Admin get teams and unassigned students; a student of the class gets the settings and counts only.</summary>
    Task<TeamFormationResponse> GetAsync(Guid classId, CancellationToken ct = default);

    Task<TeamFormationResponse> UpdateSettingsAsync(Guid classId, UpdateTeamFormationSettingsRequest request, CancellationToken ct = default);

    /// <summary>Suggested placement of the unassigned students; nothing is saved.</summary>
    Task<AutoAssignPreviewResponse> PreviewAutoAssignAsync(Guid classId, CancellationToken ct = default);

    Task<TeamFormationResponse> ApplyAssignmentsAsync(Guid classId, ApplyTeamAssignmentsRequest request, CancellationToken ct = default);

    Task<TeamFormationResponse> MoveMemberAsync(Guid classId, MoveTeamMemberRequest request, CancellationToken ct = default);
}

using SmartLab.BLL.DTOs.Teamwork;

namespace SmartLab.BLL.Interfaces.Teamwork;

/// <summary>
/// Team formation of a class, driven by its lecturer (or Admin):
/// set the size, open self-selection for students, lock it, then place the students left without a team.
/// </summary>
public interface ITeamFormationService
{
    /// <summary>Lecturer/Admin get teams and unassigned students; a student of the class gets the settings and counts only.</summary>
    Task<TeamFormationResponse> GetAsync(Guid classId, CancellationToken ct = default);

    Task<TeamFormationResponse> UpdateSettingsAsync(Guid classId, UpdateTeamFormationSettingsRequest request, CancellationToken ct = default);

    /// <summary>NotStarted or Locked → Open: students may form teams again.</summary>
    Task<TeamFormationResponse> OpenAsync(Guid classId, CancellationToken ct = default);

    /// <summary>NotStarted or Open → Locked: students can no longer change teams and every Pending invitation is rejected.</summary>
    Task<TeamFormationResponse> LockAsync(Guid classId, CancellationToken ct = default);

    /// <summary>Suggested placement of the unassigned students; nothing is saved.</summary>
    Task<AutoAssignPreviewResponse> PreviewAutoAssignAsync(Guid classId, CancellationToken ct = default);

    Task<TeamFormationResponse> ApplyAssignmentsAsync(Guid classId, ApplyTeamAssignmentsRequest request, CancellationToken ct = default);

    Task<TeamFormationResponse> MoveMemberAsync(Guid classId, MoveTeamMemberRequest request, CancellationToken ct = default);
}

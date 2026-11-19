namespace SmartLab.BLL.Constants;

/// <summary>
/// Must match the CHECK constraint on classes.team_formation_status.
/// NotStarted: students cannot form teams yet. Open: students form teams themselves (within the size limit and deadline).
/// Locked: students can no longer change teams; the lecturer places the remaining students, then teams register projects.
/// </summary>
public static class TeamFormationStatuses
{
    public const string NotStarted = "NotStarted";
    public const string Open = "Open";
    public const string Locked = "Locked";
}

public static class TeamSizeRules
{
    public const int MinTeamSize = 1;
    public const int MaxTeamSizeLimit = 10;
}

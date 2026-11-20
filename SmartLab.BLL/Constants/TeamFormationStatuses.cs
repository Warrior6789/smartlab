namespace SmartLab.BLL.Constants;

/// <summary>
/// Must match the CHECK constraint on classes.team_formation_status.
/// No longer drives any rule (students form teams until the deadline, the lecturer places students at any time);
/// kept because classes.team_formation_status still exists and is returned as is.
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

namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on borrow_handover_photos.ai_result.</summary>
public static class StudentCardCheckResults
{
    public const string Match = "Match";
    public const string Mismatch = "Mismatch";
    public const string Unreadable = "Unreadable";

    public static readonly IReadOnlyList<string> All = new[] { Match, Mismatch, Unreadable };
}

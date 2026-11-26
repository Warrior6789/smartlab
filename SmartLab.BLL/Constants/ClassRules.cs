namespace SmartLab.BLL.Constants;

/// <summary>
/// Class size bounds. The upper limit and the default size come from system_settings
/// (SystemSettingKeys.ClassMaxStudentsLimit / ClassDefaultMaxStudents); these values are used when a setting is missing.
/// </summary>
public static class ClassRules
{
    public const int MinStudents = 1;

    /// <summary>Highest limit Admin can set for CLASS_MAX_STUDENTS_LIMIT.</summary>
    public const int AbsoluteMaxStudents = 200;

    public const int FallbackMaxStudentsLimit = 35;

    public const int FallbackDefaultMaxStudents = 30;
}

namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on class_students.status.</summary>
public static class ClassStudentStatuses
{
    public const string Studying = "Studying";
    public const string Dropped = "Dropped";

    public static readonly IReadOnlyList<string> All = new[] { Studying, Dropped };
}

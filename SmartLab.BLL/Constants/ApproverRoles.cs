namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on borrow_approvals.approver_role.</summary>
public static class ApproverRoles
{
    /// <summary>The only step: the lecturer of the class, also for requests with HIGH_VALUE components.</summary>
    public const string Lecturer = "Lecturer";

    public static readonly IReadOnlyList<string> All = new[] { Lecturer };
}

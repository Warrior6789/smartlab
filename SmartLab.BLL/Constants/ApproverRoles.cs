namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on borrow_approvals.approver_role.</summary>
public static class ApproverRoles
{
    /// <summary>Step 1: the lecturer of the class.</summary>
    public const string Lecturer = "Lecturer";

    /// <summary>
    /// Step 2, only when the request has HIGH_VALUE components: the head of the lecturer's department,
    /// or an Admin when the department has no active head or the head is the class lecturer.
    /// </summary>
    public const string DepartmentHead = "DepartmentHead";

    public static readonly IReadOnlyList<string> All = new[] { Lecturer, DepartmentHead };
}

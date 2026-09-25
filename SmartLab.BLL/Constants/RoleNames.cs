namespace SmartLab.BLL.Constants;

/// <summary>Must match roles.role_name seeded in the database.</summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string LabStaff = "LabStaff";
    public const string InventoryManager = "InventoryManager";
    public const string Lecturer = "Lecturer";
    public const string Student = "Student";

    public static readonly IReadOnlyList<string> All = new[] { Admin, LabStaff, InventoryManager, Lecturer, Student };
}

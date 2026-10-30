namespace SmartLab.BLL.Services.Reports;

/// <summary>What the current user may see in reports: everything, or only some labs and classes.</summary>
public class ReportScope
{
    /// <summary>Admin and InventoryManager: every lab, every class, money figures.</summary>
    public bool SeesAll { get; init; }

    public bool IsLabStaff { get; init; }

    public bool IsLecturer { get; init; }

    /// <summary>Labs the user is assigned to as LabStaff.</summary>
    public List<Guid> RoomIds { get; init; } = new();

    /// <summary>Classes the user teaches as Lecturer.</summary>
    public List<Guid> ClassIds { get; init; } = new();
}

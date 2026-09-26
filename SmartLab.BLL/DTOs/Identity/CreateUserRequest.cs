namespace SmartLab.BLL.DTOs.Identity;

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    /// <summary>LabStaff, InventoryManager or Lecturer (students register themselves, admin is seeded).</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Required when Role is Lecturer.</summary>
    public string? InstructorCode { get; set; }
    public string? Department { get; set; }
    public string? AcademicTitle { get; set; }
}

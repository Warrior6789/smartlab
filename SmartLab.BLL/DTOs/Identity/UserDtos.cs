namespace SmartLab.BLL.DTOs.Identity;

public class UserQueryParams
{
    /// <summary>Matches username, email or full name (case-insensitive).</summary>
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

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

public class AssignRolesRequest
{
    /// <summary>Full new role list; replaces the user's current roles.</summary>
    public List<string> Roles { get; set; } = new();
}

public class UserItemResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class UserDetailResponse : UserItemResponse
{
    public string? PhoneNumber { get; set; }
    public DateTime UpdatedAt { get; set; }
    public StudentProfileResponse? StudentProfile { get; set; }
    public InstructorProfileResponse? InstructorProfile { get; set; }
}

public class StudentProfileResponse
{
    public string StudentCode { get; set; } = string.Empty;
    public string? Major { get; set; }
    public string? Cohort { get; set; }
}

public class InstructorProfileResponse
{
    public string InstructorCode { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? AcademicTitle { get; set; }
}

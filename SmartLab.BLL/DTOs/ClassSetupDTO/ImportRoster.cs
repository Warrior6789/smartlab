namespace SmartLab.BLL.DTOs.ClassSetupDTO;

public class ImportRosterResult
{
    public int TotalRows { get; set; }
    public int CreatedUsers { get; set; }
    public int EnrolledStudents { get; set; }
    public int SkippedRows { get; set; }
    public List<string> Errors { get; set; } = new();

    /// <summary>Students who already had an account (added to the class, no email sent).</summary>
    public int ExistingAccounts { get; set; }

    /// <summary>One line per data row of the file, in file order.</summary>
    public List<ImportRosterRowResult> Rows { get; set; } = new();
}


public class AssignInstructorRequest
{
    public Guid ClassId { get; set; }
    public string InstructorCode { get; set; } = null!; 
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? AcademicTitle { get; set; }

    /// <summary>
    /// The class already has another lecturer: false (default) refuses with 409 and names that lecturer, so the page
    /// can ask "replace X with Y?"; true replaces after the Admin agreed.
    /// </summary>
    public bool ReplaceExisting { get; set; }
}

public class AssignInstructorResponse
{
    public Guid InstructorProfileId { get; set; }
    public string InstructorCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? AcademicTitle { get; set; }

    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;

    /// <summary>Lecturer the class had before this assignment; null when it had none or it is the same person.</summary>
    public string? PreviousInstructorName { get; set; }

    /// <summary>A Lecturer account was created and its login was emailed.</summary>
    public bool AccountCreated { get; set; }
}
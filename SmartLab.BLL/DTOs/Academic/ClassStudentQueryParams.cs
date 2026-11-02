namespace SmartLab.BLL.DTOs.Academic;

public class ClassStudentQueryParams
{
    /// <summary>Matches student code, full name or email (case-insensitive).</summary>
    public string? Search { get; set; }

    /// <summary>Studying or Dropped; empty returns every student of the class.</summary>
    public string? Status { get; set; }
}
namespace SmartLab.BLL.External.Ai;

/// <summary>What the AI read from a student card in a handover photo.</summary>
public class StudentCardReading
{
    /// <summary>False when no student card is visible in the photo.</summary>
    public bool IsStudentCard { get; set; }

    public string? FullName { get; set; }

    public string? StudentCode { get; set; }
}

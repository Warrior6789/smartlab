namespace SmartLab.BLL.DTOs.ClassSetupDTO;

/// <summary>What happened to one row of the roster file, for the result table.</summary>
public class ImportRosterRowResult
{
    public int RowNumber { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// ExistingAccount: the student already had an account and was added to the class (no email).
    /// NewAccount: an account was created and its login was emailed.
    /// Failed: nothing changed for this row, see Message.
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    public string? Message { get; set; }
}

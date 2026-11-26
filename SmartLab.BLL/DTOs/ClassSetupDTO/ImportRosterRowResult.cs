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
    /// NewAccount: an account was created; it waits for the owner to open the activation email.
    /// Failed: nothing changed for this row, see Message.
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    public string? Message { get; set; }

    /// <summary>The account of the row; null when the row failed.</summary>
    public Guid? UserId { get; set; }

    public bool IsActive { get; set; }

    /// <summary>The account exists but its owner has not opened the activation link yet.</summary>
    public bool ActivationPending { get; set; }
}

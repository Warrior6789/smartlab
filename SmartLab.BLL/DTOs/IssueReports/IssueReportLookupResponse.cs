namespace SmartLab.BLL.DTOs.IssueReports;

/// <summary>The unit a staff member is about to record an issue for, shown before they fill the form.</summary>
public class IssueReportLookupResponse
{
    public IssueReportItemResponse Item { get; set; } = null!;

    /// <summary>Who is borrowing the unit; null when it is in its cabinet.</summary>
    public IssueReportBorrowContextResponse? BorrowContext { get; set; }

    /// <summary>Always true: only lab staff record issues and recording is final. Kept for older app versions.</summary>
    public bool ConfirmsImmediately { get; set; }

    /// <summary>Units of the same component free to give the borrower instead; always 0 when the unit is not lent.</summary>
    public int FreeReplacementUnits { get; set; }
}

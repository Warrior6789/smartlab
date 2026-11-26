namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>Internal command: lab staff checked a lent unit that is broken, burnt or lost.</summary>
public class RecordLoanIssueCommand
{
    /// <summary>The Issued per-unit line of the unit.</summary>
    public Guid BorrowItemId { get; set; }

    public string IssueType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    /// <summary>True to also give the student another unit when the request's lab has a free one (not for Lost).</summary>
    public bool Replace { get; set; }
}

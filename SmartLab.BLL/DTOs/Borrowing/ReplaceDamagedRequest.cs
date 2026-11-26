namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>The student brings back a broken unit during the loan and asks for another one of the same component.</summary>
public class ReplaceDamagedRequest
{
    /// <summary>The Issued per-unit line of the broken unit.</summary>
    public Guid BorrowItemId { get; set; }

    /// <summary>The Return photo of the broken unit uploaded for this request.</summary>
    public Guid PhotoId { get; set; }
}

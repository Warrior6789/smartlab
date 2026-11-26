namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>
/// Result of replacing a broken unit. ReplacementBorrowItemId is null when the request's lab has no free unit left: the student goes
/// without.
/// </summary>
public class ReplaceDamagedResponse
{
    public BorrowRequestDetailResponse Request { get; set; } = null!;

    public Guid? ReplacementBorrowItemId { get; set; }
}

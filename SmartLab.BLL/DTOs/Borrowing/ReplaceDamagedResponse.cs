namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>
/// Result of replacing a broken unit. ReplacementBorrowItemId is null when no lab has a free unit left: the student goes
/// without. WaitingTransfer is true when the replacement must first be carried from SourceRoomCode to the pickup lab.
/// </summary>
public class ReplaceDamagedResponse
{
    public BorrowRequestDetailResponse Request { get; set; } = null!;

    public Guid? ReplacementBorrowItemId { get; set; }

    public Guid? SourceRoomId { get; set; }

    public string? SourceRoomCode { get; set; }

    public bool WaitingTransfer { get; set; }
}

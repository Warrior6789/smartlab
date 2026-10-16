namespace SmartLab.BLL.DTOs.Borrowing;

public class HandoverPhotoResponse
{
    public Guid PhotoId { get; set; }

    public Guid BorrowRequestId { get; set; }

    /// <summary>Issue (handing over) or Return.</summary>
    public string PhotoType { get; set; } = string.Empty;

    public string PhotoUrl { get; set; } = string.Empty;

    /// <summary>Issue photos only: Match, Mismatch or Unreadable (student card vs borrower).</summary>
    public string? AiResult { get; set; }

    /// <summary>Issue photos only: what the AI read (JSON text).</summary>
    public string? AiDetail { get; set; }

    /// <summary>Why the staff handed over although the AI did not confirm the card.</summary>
    public string? OverrideReason { get; set; }

    public Guid TakenBy { get; set; }

    public string TakenByName { get; set; } = string.Empty;

    public DateTime TakenAt { get; set; }
}

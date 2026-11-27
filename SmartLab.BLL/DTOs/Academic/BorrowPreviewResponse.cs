namespace SmartLab.BLL.DTOs.Academic;

/// <summary>The borrow requests sending would create now, one per lab. Nothing is saved.</summary>
public class BorrowPreviewResponse
{
    public List<BorrowPreviewLabResponse> Labs { get; set; } = new();

    /// <summary>Why sending would fail now (a lab lacks stock, no lab keeps a component); null when it would go through.</summary>
    public string? Problem { get; set; }
}

namespace SmartLab.BLL.DTOs.Reports;

public class OverdueBorrowResponse
{
    public Guid BorrowRequestId { get; set; }
    public string RequestCode { get; set; } = null!;
    public string RequesterName { get; set; } = null!;
    public string RequesterUsername { get; set; } = null!;
    public string? ClassCode { get; set; }
    public string? ProjectTitle { get; set; }
    public string RoomCode { get; set; } = null!;
    public DateTime DueDate { get; set; }

    /// <summary>Whole days since the last day to return ended.</summary>
    public int DaysOverdue { get; set; }

    /// <summary>Lines still issued (not yet returned).</summary>
    public List<OverdueItemResponse> OutstandingItems { get; set; } = new();
}

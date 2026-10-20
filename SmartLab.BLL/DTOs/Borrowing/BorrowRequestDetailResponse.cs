namespace SmartLab.BLL.DTOs.Borrowing;

public class BorrowRequestDetailResponse : BorrowRequestResponse
{
    public string? StudentCode { get; set; }

    public string Purpose { get; set; } = string.Empty;

    /// <summary>Why the lecturer rejected, or why staff cancelled the request.</summary>
    public string? RejectReason { get; set; }

    public List<BorrowItemResponse> Items { get; set; } = new();

    public List<HandoverPhotoResponse> Photos { get; set; } = new();

    public List<BorrowApprovalResponse> Approvals { get; set; } = new();

    /// <summary>Due date extensions asked for this request, oldest first.</summary>
    public List<BorrowExtensionResponse> Extensions { get; set; } = new();
}

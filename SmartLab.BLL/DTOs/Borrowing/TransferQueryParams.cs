namespace SmartLab.BLL.DTOs.Borrowing;

public class TransferQueryParams
{
    /// <summary>Only lines taken from this lab.</summary>
    public Guid? SourceRoomId { get; set; }

    /// <summary>Request code or component code / name.</summary>
    public string? Search { get; set; }
}

namespace SmartLab.BLL.DTOs.Academic;

public class BorrowPreviewLineResponse
{
    public Guid ComponentId { get; set; }

    /// <summary>Short code shown to users (e.g. CBNDDA-DHT22), the system code when there is none.</summary>
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

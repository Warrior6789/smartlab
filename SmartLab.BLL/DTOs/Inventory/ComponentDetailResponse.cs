namespace SmartLab.BLL.DTOs.Inventory;

public class ComponentDetailResponse : ComponentResponse
{
    public string? Description { get; set; }
    public string? DatasheetUrl { get; set; }

    /// <summary>Image of the first page; only set when the datasheet is an uploaded PDF (not a pasted link).</summary>
    public string? DatasheetPreviewUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<ComponentSpecResponse> Specs { get; set; } = Array.Empty<ComponentSpecResponse>();
    public IReadOnlyList<ComponentImageResponse> Images { get; set; } = Array.Empty<ComponentImageResponse>();
    public IReadOnlyList<CabinetStockResponse> Stocks { get; set; } = Array.Empty<CabinetStockResponse>();
}

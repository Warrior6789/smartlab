namespace SmartLab.BLL.DTOs.Inventory;

public class CabinetDetailResponse : CabinetResponse
{
    public IReadOnlyList<CabinetContentResponse> Contents { get; set; } = Array.Empty<CabinetContentResponse>();
}

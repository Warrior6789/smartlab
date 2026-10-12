namespace SmartLab.BLL.DTOs.Academic;

public class ProjectComponentRequest
{
    public Guid ComponentId { get; set; }

    public int Quantity { get; set; }

    public string? Note { get; set; }
}

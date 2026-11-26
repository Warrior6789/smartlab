namespace SmartLab.BLL.DTOs.Reports;

public class CountGroupResponse
{
    public string Key { get; set; } = null!;
    /// <summary>Display name when the key is a code; null otherwise.</summary>
    public string? Name { get; set; }

    public int Count { get; set; }
}

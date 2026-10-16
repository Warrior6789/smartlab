namespace SmartLab.BLL.DTOs.Academic;

public class SuggestComponentsRequest
{
    /// <summary>What the project does; the AI reads only this text. Stock is checked over all labs.</summary>
    public string Description { get; set; } = string.Empty;
}

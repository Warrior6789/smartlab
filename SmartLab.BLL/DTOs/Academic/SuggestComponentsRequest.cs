namespace SmartLab.BLL.DTOs.Academic;

public class SuggestComponentsRequest
{
    /// <summary>What the project does; the AI reads only this text.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Class of the project. When given, stock is checked only in the lab of the class's course (the lab the request
    /// will go to); without it, in the lab that has the most of the suggested components.
    /// </summary>
    public Guid? ClassId { get; set; }
}

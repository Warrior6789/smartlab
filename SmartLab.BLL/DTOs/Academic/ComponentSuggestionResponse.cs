namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// AI suggestion split in three groups. Nothing is saved: the team leader picks what to keep and saves it with
/// PUT /api/projects/{id} (Components + SelfSuppliedItems).
/// </summary>
public class ComponentSuggestionResponse
{
    public Guid RoomId { get; set; }

    public string RoomCode { get; set; } = string.Empty;

    /// <summary>In the catalog and the lab has enough now.</summary>
    public List<SuggestedComponentResponse> Borrowable { get; set; } = new();

    /// <summary>In the catalog but the lab has less than suggested (lower the quantity, pick another lab or buy the rest).</summary>
    public List<NotEnoughStockComponentResponse> NotEnoughStock { get; set; } = new();

    /// <summary>Not in the catalog: the team buys these itself.</summary>
    public List<SelfSuppliedSuggestionResponse> SelfSupplied { get; set; } = new();

    /// <summary>
    /// Set only when all three lists are empty (description too vague, no hardware needed, or not allowed),
    /// so the UI can tell the student how to rewrite the description.
    /// </summary>
    public string? Hint { get; set; }
}

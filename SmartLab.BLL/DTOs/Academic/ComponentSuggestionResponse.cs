namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// AI suggestion split in three groups by the stock of one lab (<see cref="Lab"/>), because a project borrows everything
/// from a single lab and labs never pass components to each other. Nothing is saved: the team leader picks what to keep
/// and saves it with PUT /api/projects/{id} (Components + SelfSuppliedItems).
/// </summary>
public class ComponentSuggestionResponse
{
    /// <summary>The lab with the most of the suggested components; null when no lab can lend or nothing is in the catalog.</summary>
    public SuggestedLabResponse? Lab { get; set; }

    /// <summary>In the catalog and <see cref="Lab"/> has enough now.</summary>
    public List<SuggestedComponentResponse> Borrowable { get; set; } = new();

    /// <summary>In the catalog and <see cref="Lab"/> has some, but less than suggested (lower the quantity or buy the rest).</summary>
    public List<NotEnoughStockComponentResponse> NotEnoughStock { get; set; } = new();

    /// <summary>
    /// The team buys these itself: items not in the catalog, and catalog components <see cref="Lab"/> does not have
    /// (even when another lab has them).
    /// </summary>
    public List<SelfSuppliedSuggestionResponse> SelfSupplied { get; set; } = new();

    /// <summary>
    /// Set only when all three lists are empty (description too vague, no hardware needed, or not allowed),
    /// so the UI can tell the student how to rewrite the description.
    /// </summary>
    public string? Hint { get; set; }
}

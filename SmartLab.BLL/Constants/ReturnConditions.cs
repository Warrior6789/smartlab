namespace SmartLab.BLL.Constants;

/// <summary>Condition of a per-unit component when it is returned (borrow_items.return_condition).</summary>
public static class ReturnConditions
{
    public const string Good = "Good";
    public const string Damaged = "Damaged";
    public const string Lost = "Lost";

    public static readonly IReadOnlyList<string> All = new[] { Good, Damaged, Lost };

    /// <summary>"good" / " GOOD " → "Good"; null when the text is not a condition.</summary>
    public static string? Normalize(string? condition) => StatusLookup.Find(All, condition);
}

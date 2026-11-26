namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on inventory_transactions.type.</summary>
public static class InventoryTransactionTypes
{
    public const string Receipt = "Receipt";
    public const string ReceiptCancel = "ReceiptCancel";
    public const string Adjust = "Adjust";
    public const string Move = "Move";
    public const string Retire = "Retire";
    public const string Borrow = "Borrow";
    public const string Return = "Return";

    public static readonly IReadOnlyList<string> All = new[] { Receipt, ReceiptCancel, Adjust, Move, Retire, Borrow, Return };

    /// <summary>"move" / " MOVE " → "Move"; null when the text is not a type.</summary>
    public static string? Normalize(string? type) => StatusLookup.Find(All, type);
}

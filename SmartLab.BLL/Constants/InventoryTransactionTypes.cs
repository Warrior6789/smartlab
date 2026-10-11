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
}

namespace SmartLab.BLL.Services.Reports;

/// <summary>One ledger line of a component, as the stock card report needs it.</summary>
public class MovementLine
{
    public string Type { get; init; } = null!;

    public int QuantityChange { get; init; }

    public Guid? ItemId { get; init; }

    public Guid? ReceiptId { get; init; }

    public DateTime CreatedAt { get; init; }

    public string? Reason { get; init; }
}

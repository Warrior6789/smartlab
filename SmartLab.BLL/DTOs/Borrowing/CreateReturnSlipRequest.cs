namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>The borrower asks to bring back all or some of the components still out; the lab picks the day when approving.</summary>
public class CreateReturnSlipRequest
{
    public string? Note { get; set; }

    public List<ReturnSlipLineRequest> Lines { get; set; } = new();
}

using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class BorrowReturnSlipItem
{
    public Guid ReturnSlipId { get; set; }

    public Guid BorrowItemId { get; set; }

    public int Quantity { get; set; }

    public virtual BorrowItem BorrowItem { get; set; } = null!;

    public virtual BorrowReturnSlip ReturnSlip { get; set; } = null!;
}

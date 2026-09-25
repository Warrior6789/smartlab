using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class BorrowExtension
{
    public Guid ExtensionId { get; set; }

    public Guid BorrowRequestId { get; set; }

    public DateTime OldDueDate { get; set; }

    public DateTime NewDueDate { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public Guid? ReviewedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public virtual BorrowRequest BorrowRequest { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }
}

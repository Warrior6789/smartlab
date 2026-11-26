using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class BorrowReturnSlip
{
    public Guid ReturnSlipId { get; set; }

    public string SlipCode { get; set; } = null!;

    public Guid BorrowRequestId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? AppointmentDate { get; set; }

    public string? Note { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewComment { get; set; }

    public Guid? ReceivedBy { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual BorrowRequest BorrowRequest { get; set; } = null!;

    public virtual ICollection<BorrowReturnSlipItem> BorrowReturnSlipItems { get; set; } = new List<BorrowReturnSlipItem>();

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual User? ReceivedByNavigation { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }
}

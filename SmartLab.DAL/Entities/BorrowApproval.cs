using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class BorrowApproval
{
    public Guid ApprovalId { get; set; }

    public Guid BorrowRequestId { get; set; }

    public short StepNo { get; set; }

    public string ApproverRole { get; set; } = null!;

    public Guid ApproverId { get; set; }

    public string Status { get; set; } = null!;

    public string? Comment { get; set; }

    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Approver { get; set; } = null!;

    public virtual BorrowRequest BorrowRequest { get; set; } = null!;
}

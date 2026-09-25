using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class BorrowRequest
{
    public Guid BorrowRequestId { get; set; }

    public string RequestCode { get; set; } = null!;

    public Guid RequesterId { get; set; }

    public Guid? ApproverId { get; set; }

    public Guid? ClassId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Purpose { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime RequestDate { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? BorrowDate { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public string? RejectReason { get; set; }

    public virtual User? Approver { get; set; }

    public virtual ICollection<BorrowExtension> BorrowExtensions { get; set; } = new List<BorrowExtension>();

    public virtual ICollection<BorrowItem> BorrowItems { get; set; } = new List<BorrowItem>();

    public virtual Class? Class { get; set; }

    public virtual Project? Project { get; set; }

    public virtual User Requester { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class BorrowRequest
{
    public Guid BorrowRequestId { get; set; }

    public string RequestCode { get; set; } = null!;

    public Guid RequesterId { get; set; }

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

    public Guid RoomId { get; set; }

    public virtual ICollection<BorrowApproval> BorrowApprovals { get; set; } = new List<BorrowApproval>();

    public virtual ICollection<BorrowExtension> BorrowExtensions { get; set; } = new List<BorrowExtension>();

    public virtual ICollection<BorrowHandoverPhoto> BorrowHandoverPhotos { get; set; } = new List<BorrowHandoverPhoto>();

    public virtual ICollection<BorrowItem> BorrowItems { get; set; } = new List<BorrowItem>();

    public virtual Class? Class { get; set; }

    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public virtual Project? Project { get; set; }

    public virtual User Requester { get; set; } = null!;

    public virtual LabRoom Room { get; set; } = null!;
}

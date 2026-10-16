using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class BorrowHandoverPhoto
{
    public Guid PhotoId { get; set; }

    public Guid BorrowRequestId { get; set; }

    public string PhotoType { get; set; } = null!;

    public string PhotoUrl { get; set; } = null!;

    public string StorageKey { get; set; } = null!;

    public string? AiResult { get; set; }

    public string? AiDetail { get; set; }

    public string? OverrideReason { get; set; }

    public Guid TakenBy { get; set; }

    public DateTime TakenAt { get; set; }

    public virtual ICollection<BorrowItem> BorrowItems { get; set; } = new List<BorrowItem>();

    public virtual BorrowRequest BorrowRequest { get; set; } = null!;

    public virtual User TakenByNavigation { get; set; } = null!;
}

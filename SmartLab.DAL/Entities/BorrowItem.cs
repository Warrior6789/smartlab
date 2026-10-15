using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class BorrowItem
{
    public Guid BorrowItemId { get; set; }

    public Guid BorrowRequestId { get; set; }

    public Guid ComponentId { get; set; }

    public Guid? ComponentItemId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? IssuedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public string? ReturnCondition { get; set; }

    public int Quantity { get; set; }

    public int? ReturnedQuantity { get; set; }

    public Guid? SourceRoomId { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public Guid? ReceivedBy { get; set; }

    public Guid? ReturnPhotoId { get; set; }

    public virtual BorrowRequest BorrowRequest { get; set; } = null!;

    public virtual Component Component { get; set; } = null!;

    public virtual ComponentItem? ComponentItem { get; set; }

    public virtual ICollection<IssueReport> IssueReports { get; set; } = new List<IssueReport>();

    public virtual User? ReceivedByNavigation { get; set; }

    public virtual BorrowHandoverPhoto? ReturnPhoto { get; set; }

    public virtual LabRoom? SourceRoom { get; set; }
}

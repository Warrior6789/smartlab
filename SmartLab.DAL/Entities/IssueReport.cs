using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class IssueReport
{
    public Guid IssueReportId { get; set; }

    public Guid? BorrowItemId { get; set; }

    public Guid ComponentItemId { get; set; }

    public Guid ReportedBy { get; set; }

    public string IssueType { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public string Status { get; set; } = null!;

    public decimal? CompensationAmount { get; set; }

    public Guid? ResolvedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual BorrowItem? BorrowItem { get; set; }

    public virtual ComponentItem ComponentItem { get; set; } = null!;

    public virtual User ReportedByNavigation { get; set; } = null!;

    public virtual User? ResolvedByNavigation { get; set; }
}

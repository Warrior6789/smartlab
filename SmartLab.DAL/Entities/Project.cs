using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class Project
{
    public Guid ProjectId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public Guid OwnerId { get; set; }

    public Guid? ClassId { get; set; }

    public string? GithubUrl { get; set; }

    public string? SlideUrl { get; set; }

    public string? DemoVideoUrl { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? TeamId { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewComment { get; set; }

    public string? FinalStatus { get; set; }

    public DateTime? FinalSubmittedAt { get; set; }

    public Guid? FinalReviewedBy { get; set; }

    public DateTime? FinalReviewedAt { get; set; }

    public string? FinalComment { get; set; }

    public string? ReportUrl { get; set; }

    public string? ReportStorageKey { get; set; }

    public string? SourceCodeUrl { get; set; }

    public string? SourceCodeStorageKey { get; set; }

    public virtual ICollection<BorrowRequest> BorrowRequests { get; set; } = new List<BorrowRequest>();

    public virtual Class? Class { get; set; }

    public virtual User? FinalReviewedByNavigation { get; set; }

    public virtual User Owner { get; set; } = null!;

    public virtual ICollection<ProjectComponent> ProjectComponents { get; set; } = new List<ProjectComponent>();

    public virtual ICollection<ProjectSelfSuppliedItem> ProjectSelfSuppliedItems { get; set; } = new List<ProjectSelfSuppliedItem>();

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual Team? Team { get; set; }
}

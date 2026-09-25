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

    public virtual ICollection<BorrowRequest> BorrowRequests { get; set; } = new List<BorrowRequest>();

    public virtual Class? Class { get; set; }

    public virtual User Owner { get; set; } = null!;

    public virtual ICollection<ProjectComponent> ProjectComponents { get; set; } = new List<ProjectComponent>();
}

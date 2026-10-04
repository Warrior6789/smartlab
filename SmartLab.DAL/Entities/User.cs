using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class User
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool EmailVerified { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<BorrowExtension> BorrowExtensions { get; set; } = new List<BorrowExtension>();

    public virtual ICollection<BorrowRequest> BorrowRequestApprovers { get; set; } = new List<BorrowRequest>();

    public virtual ICollection<BorrowRequest> BorrowRequestRequesters { get; set; } = new List<BorrowRequest>();

    public virtual ICollection<ComponentImage> ComponentImages { get; set; } = new List<ComponentImage>();

    public virtual ICollection<ComponentTutorial> ComponentTutorials { get; set; } = new List<ComponentTutorial>();

    public virtual InstructorProfile? InstructorProfile { get; set; }

    public virtual ICollection<InventoryAudit> InventoryAudits { get; set; } = new List<InventoryAudit>();

    public virtual ICollection<IssueReport> IssueReportReportedByNavigations { get; set; } = new List<IssueReport>();

    public virtual ICollection<IssueReport> IssueReportResolvedByNavigations { get; set; } = new List<IssueReport>();

    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    public virtual ICollection<StockReceipt> StockReceipts { get; set; } = new List<StockReceipt>();

    public virtual StudentProfile? StudentProfile { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public virtual ICollection<VerificationCode> VerificationCodes { get; set; } = new List<VerificationCode>();
}

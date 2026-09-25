using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = null!;

    public string EntityName { get; set; } = null!;

    public string? EntityId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public bool IsSuccess { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}

namespace SmartLab.BLL.DTOs.AuditLogs;

public class AuditLogResponse
{
    public long AuditLogId { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string Action { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public string? EntityId { get; set; }

    /// <summary>JSON of the values before the change.</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON of the values after the change.</summary>
    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime CreatedAt { get; set; }
}

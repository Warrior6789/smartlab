namespace SmartLab.BLL.DTOs.AuditLogs;

public class AuditLogQueryParams
{
    public Guid? UserId { get; set; }

    /// <summary>LOGIN, CHANGE_PASSWORD, CREATE_USER, UPDATE_USER, ASSIGN_ROLES, RESET_PASSWORD, ADD_PERMISSION, REMOVE_PERMISSION or UPDATE_SETTING.</summary>
    public string? Action { get; set; }

    /// <summary>User, Role or SystemSetting.</summary>
    public string? EntityName { get; set; }

    public string? EntityId { get; set; }

    public bool? IsSuccess { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd) of CreatedAt, inclusive.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd) of CreatedAt, inclusive.</summary>
    public DateTime? ToDate { get; set; }
}

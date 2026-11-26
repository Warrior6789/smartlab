namespace SmartLab.BLL.Interfaces.AuditLogs;

/// <summary>
/// Adds an audit_logs row to the current unit of work without saving, so it is saved together with the change it
/// describes by the caller's SaveChangesAsync.
/// </summary>
public interface IAuditLogger
{
    /// <param name="userId">Who did it; the current user when null.</param>
    Task AddAsync(
        string action, string entityName, string? entityId, object? oldValue = null, object? newValue = null,
        bool isSuccess = true, Guid? userId = null, CancellationToken ct = default);
}

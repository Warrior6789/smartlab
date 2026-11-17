using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.AuditLogs;

namespace SmartLab.BLL.Interfaces.AuditLogs;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogResponse>> GetLogsAsync(
        AuditLogQueryParams query, PageParams paging, CancellationToken ct = default);
}

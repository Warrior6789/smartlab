using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.AuditLogs;
using SmartLab.BLL.Interfaces.AuditLogs;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.AuditLogs;

public class AuditLogService : IAuditLogService
{
    private readonly IUnitOfWork _uow;

    public AuditLogService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public Task<PagedResult<AuditLogResponse>> GetLogsAsync(
        AuditLogQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var logs = _uow.Repository<AuditLog>().QueryNoTracking();

        if (query.UserId.HasValue)
            logs = logs.Where(l => l.UserId == query.UserId.Value);

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim().ToUpper();
            logs = logs.Where(l => l.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            var entityName = query.EntityName.Trim().ToLower();
            logs = logs.Where(l => l.EntityName.ToLower() == entityName);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            var entityId = query.EntityId.Trim().ToLower();
            logs = logs.Where(l => l.EntityId != null && l.EntityId.ToLower() == entityId);
        }

        if (query.IsSuccess.HasValue)
            logs = logs.Where(l => l.IsSuccess == query.IsSuccess.Value);

        if (query.FromDate.HasValue)
        {
            var from = VietnamDays.StartUtc(query.FromDate.Value);
            logs = logs.Where(l => l.CreatedAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var toExclusive = VietnamDays.EndExclusiveUtc(query.ToDate.Value);
            logs = logs.Where(l => l.CreatedAt < toExclusive);
        }

        var result = logs
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.AuditLogId)
            .Select(l => new AuditLogResponse
            {
                AuditLogId = l.AuditLogId,
                UserId = l.UserId,
                Username = l.User != null ? l.User.Username : null,
                FullName = l.User != null ? l.User.FullName : null,
                Action = l.Action,
                EntityName = l.EntityName,
                EntityId = l.EntityId,
                OldValue = l.OldValue,
                NewValue = l.NewValue,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                IsSuccess = l.IsSuccess,
                CreatedAt = l.CreatedAt,
            });

        return PagedResult<AuditLogResponse>.CreateAsync(result, paging, ct);
    }
}

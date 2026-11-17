using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.AuditLogs;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.AuditLogs;

public class AuditLogger : IAuditLogger
{
    private const int MaxEntityIdLength = 50;
    private const int MaxIpAddressLength = 45;
    private const int MaxUserAgentLength = 500;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _accessor;

    public AuditLogger(IUnitOfWork uow, ICurrentUserService currentUser, IHttpContextAccessor accessor)
    {
        _uow = uow;
        _currentUser = currentUser;
        _accessor = accessor;
    }

    public async Task AddAsync(
        string action, string entityName, string? entityId, object? oldValue = null, object? newValue = null,
        bool isSuccess = true, Guid? userId = null, CancellationToken ct = default)
    {
        var http = _accessor.HttpContext;
        string? ipAddress = null;
        string? userAgent = null;
        if (http != null)
        {
            ipAddress = http.Connection.RemoteIpAddress?.ToString();
            userAgent = http.Request.Headers.UserAgent.ToString();
        }

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            UserId = userId ?? _currentUser.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = Truncate(entityId, MaxEntityIdLength),
            OldValue = ToJson(oldValue),
            NewValue = ToJson(newValue),
            IpAddress = Truncate(ipAddress, MaxIpAddressLength),
            UserAgent = Truncate(userAgent, MaxUserAgentLength),
            IsSuccess = isSuccess,
            CreatedAt = DateTime.UtcNow,
        }, ct);
    }

    private static string? ToJson(object? value)
    {
        if (value == null)
            return null;

        return JsonSerializer.Serialize(value);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }
}

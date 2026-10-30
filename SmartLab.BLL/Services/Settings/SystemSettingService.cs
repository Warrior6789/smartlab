using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Settings;
using SmartLab.BLL.Interfaces.AuditLogs;
using SmartLab.BLL.Interfaces.Settings;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Settings;

public class SystemSettingService : ISystemSettingService
{
    private const int MaxDaysOrTimes = 365;

    /// <summary>Keys Admin can change, in display order, with the description saved on a new row.</summary>
    private static readonly Dictionary<string, string> EditableKeys = new()
    {
        [SystemSettingKeys.PickupRoomId] = "Lab nhận đồ chung, nơi sinh viên nhận và trả linh kiện",
        [SystemSettingKeys.BorrowMaxDays] = "Số ngày mượn tối đa tính từ ngày mượn đến hạn trả",
        [SystemSettingKeys.BorrowExtensionMaxDays] = "Số ngày tối đa mỗi lần gia hạn",
        [SystemSettingKeys.BorrowExtensionMaxTimes] = "Số lần gia hạn tối đa của một đơn mượn",
        [SystemSettingKeys.BorrowReminderHour] = "Giờ (0-23, giờ Việt Nam) gửi email nhắc hạn trả mỗi ngày",
    };

    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _auditLogger;

    public SystemSettingService(IUnitOfWork uow, IAuditLogger auditLogger)
    {
        _uow = uow;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<SystemSettingResponse>> GetSettingsAsync(CancellationToken ct = default)
    {
        var keys = EditableKeys.Keys.ToList();
        var rows = await _uow.Repository<SystemSetting>().QueryNoTracking()
            .Where(s => keys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, ct);

        var settings = new List<SystemSettingResponse>();
        foreach (var key in keys)
        {
            rows.TryGetValue(key, out var row);
            settings.Add(ToResponse(key, row));
        }

        return settings;
    }

    public async Task<SystemSettingResponse> UpdateSettingAsync(
        string key, UpdateSystemSettingRequest request, CancellationToken ct = default)
    {
        var normalizedKey = key.Trim().ToUpperInvariant();
        if (!EditableKeys.ContainsKey(normalizedKey))
            throw new NotFoundException($"Không có cài đặt '{key}' hoặc cài đặt này không được sửa");

        var value = await CheckValueAsync(normalizedKey, request.Value.Trim(), ct);

        var settings = _uow.Repository<SystemSetting>();
        var row = await settings.Query().FirstOrDefaultAsync(s => s.SettingKey == normalizedKey, ct);
        var now = DateTime.UtcNow;
        string? oldValue = null;
        if (row == null)
        {
            row = new SystemSetting
            {
                SettingKey = normalizedKey,
                SettingValue = value,
                Description = EditableKeys[normalizedKey],
                UpdatedAt = now,
            };
            await settings.AddAsync(row, ct);
        }
        else
        {
            oldValue = row.SettingValue;
            row.SettingValue = value;
            row.UpdatedAt = now;
        }

        await _auditLogger.AddAsync(AuditActions.UpdateSetting, nameof(SystemSetting), normalizedKey,
            new { Value = oldValue }, new { Value = value }, ct: ct);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Cài đặt vừa được thay đổi bởi người khác, vui lòng tải lại và thử lại");
        }

        return ToResponse(normalizedKey, row);
    }

    /// <summary>The value to save for <paramref name="key"/>; 400 when it does not fit the key.</summary>
    private async Task<string> CheckValueAsync(string key, string value, CancellationToken ct)
    {
        if (key == SystemSettingKeys.PickupRoomId)
        {
            if (!Guid.TryParse(value, out var roomId))
                throw new BadRequestException("Lab nhận đồ phải là id của một phòng lab");

            var active = await _uow.Repository<LabRoom>().QueryNoTracking()
                .AnyAsync(r => r.RoomId == roomId && r.Status == LabRoomStatuses.Active, ct);
            if (!active)
                throw new BadRequestException("Phòng lab không tồn tại hoặc đã ngừng sử dụng");

            return roomId.ToString();
        }

        if (!int.TryParse(value, out var number))
            throw new BadRequestException("Giá trị phải là số nguyên");

        if (key == SystemSettingKeys.BorrowReminderHour)
        {
            if (number < 0 || number > 23)
                throw new BadRequestException("Giờ gửi nhắc phải từ 0 đến 23");
        }
        else if (number < 1 || number > MaxDaysOrTimes)
        {
            throw new BadRequestException($"Giá trị phải từ 1 đến {MaxDaysOrTimes}");
        }

        return number.ToString();
    }

    private static SystemSettingResponse ToResponse(string key, SystemSetting? row)
    {
        if (row == null)
        {
            return new SystemSettingResponse
            {
                Key = key,
                Description = EditableKeys[key],
            };
        }

        return new SystemSettingResponse
        {
            Key = key,
            Value = row.SettingValue,
            Description = row.Description ?? EditableKeys[key],
            UpdatedAt = row.UpdatedAt,
        };
    }
}

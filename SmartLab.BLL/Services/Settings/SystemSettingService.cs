using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Settings;
using SmartLab.BLL.Interfaces.AuditLogs;
using SmartLab.BLL.Interfaces.Settings;
using SmartLab.BLL.Services.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Settings;

public class SystemSettingService : ISystemSettingService
{
    private const int MaxDaysOrTimes = 365;

    /// <summary>Keys Admin can change, in display order, with the description saved on a new row.</summary>
    private static readonly Dictionary<string, string> EditableKeys = new()
    {
        [SystemSettingKeys.BorrowDaysBeforeSemesterEnd] = "Hạn trả linh kiện sớm hơn ngày kết thúc học kỳ bao nhiêu ngày",
        [SystemSettingKeys.BorrowReminderHour] = "Giờ (0-23, giờ Việt Nam) gửi email nhắc hạn trả mỗi ngày",
        [SystemSettingKeys.ClassMaxStudentsLimit] = "Sĩ số tối đa được phép đặt cho một lớp",
        [SystemSettingKeys.ClassDefaultMaxStudents] = "Sĩ số mặc định khi tạo lớp mà không nhập sĩ số",
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
        if (!int.TryParse(value, out var number))
            throw new BadRequestException("Giá trị phải là số nguyên");

        if (key == SystemSettingKeys.BorrowReminderHour)
        {
            if (number < 0 || number > 23)
                throw new BadRequestException("Giờ gửi nhắc phải từ 0 đến 23");
        }
        else if (key == SystemSettingKeys.BorrowDaysBeforeSemesterEnd)
        {
            if (number < 0 || number > MaxDaysOrTimes)
                throw new BadRequestException($"Giá trị phải từ 0 đến {MaxDaysOrTimes}");
        }
        else if (key == SystemSettingKeys.ClassMaxStudentsLimit)
        {
            if (number < ClassRules.MinStudents || number > ClassRules.AbsoluteMaxStudents)
                throw new BadRequestException($"Sĩ số tối đa phải từ {ClassRules.MinStudents} đến {ClassRules.AbsoluteMaxStudents}");

            var defaultSize = await ClassSizeRules.GetDefaultMaxStudentsAsync(_uow, ct);
            if (number < defaultSize)
                throw new BadRequestException($"Sĩ số tối đa không được nhỏ hơn sĩ số mặc định ({defaultSize}). Hãy giảm sĩ số mặc định trước");
        }
        else if (key == SystemSettingKeys.ClassDefaultMaxStudents)
        {
            var limit = await ClassSizeRules.GetMaxStudentsLimitAsync(_uow, ct);
            if (number < ClassRules.MinStudents || number > limit)
                throw new BadRequestException($"Sĩ số mặc định phải từ {ClassRules.MinStudents} đến {limit} (sĩ số tối đa hiện tại)");
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

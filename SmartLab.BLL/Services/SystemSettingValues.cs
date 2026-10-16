using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services;

/// <summary>Reads business rules that Admin can change in system_settings (keys in SystemSettingKeys).</summary>
internal static class SystemSettingValues
{
    /// <summary>The whole number stored under <paramref name="key"/>; null when the row is missing or not a number.</summary>
    public static async Task<int?> GetIntAsync(IUnitOfWork uow, string key, CancellationToken ct)
    {
        var value = await uow.Repository<SystemSetting>().QueryNoTracking()
            .Where(s => s.SettingKey == key)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);
        if (!int.TryParse(value, out var number))
            return null;

        return number;
    }

    /// <summary>A positive whole number; 400 telling the user to contact Admin when it is missing or invalid.</summary>
    public static async Task<int> GetPositiveIntAsync(IUnitOfWork uow, string key, string settingName, CancellationToken ct)
    {
        var number = await GetIntAsync(uow, key, ct);
        if (number == null || number <= 0)
            throw new BadRequestException($"Chưa cấu hình {settingName}, vui lòng báo Admin");

        return number.Value;
    }
}

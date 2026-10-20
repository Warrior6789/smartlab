using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>The common lab where students pick up and return borrowed components (system_settings PICKUP_ROOM_ID).</summary>
internal static class PickupRoom
{
    /// <summary>The configured pickup lab; 400 when it is missing, unknown or inactive.</summary>
    public static async Task<Guid> GetActiveIdAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var value = await uow.Repository<SystemSetting>().QueryNoTracking()
            .Where(s => s.SettingKey == SystemSettingKeys.PickupRoomId)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);
        if (!Guid.TryParse(value, out var roomId))
            throw new BadRequestException("Chưa cấu hình lab nhận đồ, vui lòng báo Admin");

        var active = await uow.Repository<LabRoom>().QueryNoTracking()
            .AnyAsync(r => r.RoomId == roomId && r.Status == LabRoomStatuses.Active, ct);
        if (!active)
            throw new BadRequestException("Lab nhận đồ đang ngừng hoạt động, vui lòng báo Admin");

        return roomId;
    }
}

using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

/// <summary>Each lab takes in only the components on its list (lab_room_components); a component on no list cannot be received.</summary>
internal static class LabComponents
{
    private const int MaxNamesShown = 5;

    /// <summary>400 when one of the components is not on the lab's list, so it cannot be received into that lab.</summary>
    public static async Task EnsureReceivableAsync(
        IUnitOfWork uow, Guid roomId, IReadOnlyCollection<Guid> componentIds, CancellationToken ct)
    {
        var missing = await MissingNamesAsync(uow, roomId, componentIds, ct);
        if (missing.Count == 0)
            return;

        var roomCode = await RoomCodeAsync(uow, roomId, ct);
        throw new BadRequestException(
            $"{roomCode} không nhận {JoinNames(missing)}. Quản lý kho thêm linh kiện vào danh sách của lab trước khi nhập");
    }

    private static async Task<List<string>> MissingNamesAsync(
        IUnitOfWork uow, Guid roomId, IReadOnlyCollection<Guid> componentIds, CancellationToken ct)
    {
        if (componentIds.Count == 0)
            return new List<string>();

        return await uow.Repository<Component>().QueryNoTracking()
            .Where(c => componentIds.Contains(c.ComponentId)
                        && !c.LabRoomComponents.Any(l => l.RoomId == roomId))
            .OrderBy(c => c.ComponentCode)
            .Select(c => c.ComponentName)
            .ToListAsync(ct);
    }

    private static async Task<string> RoomCodeAsync(IUnitOfWork uow, Guid roomId, CancellationToken ct)
    {
        return await uow.Repository<LabRoom>().QueryNoTracking()
                   .Where(r => r.RoomId == roomId)
                   .Select(r => r.RoomCode)
                   .FirstOrDefaultAsync(ct)
               ?? "Lab";
    }

    private static string JoinNames(List<string> names)
    {
        if (names.Count <= MaxNamesShown)
            return string.Join(", ", names);
        return $"{string.Join(", ", names.Take(MaxNamesShown))} và {names.Count - MaxNamesShown} linh kiện khác";
    }
}

using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

/// <summary>
/// What a lab can still lend: stock in the lab's cabinets (CONSUMABLE quantity + Available units) minus what Approved
/// borrow requests of that lab already reserve (approved but not issued yet). Used when submitting, approving and
/// suggesting, so "còn" means the same everywhere.
/// </summary>
internal static class RoomStock
{
    /// <summary>Available quantity per component (never negative); components with nothing left are absent.</summary>
    public static async Task<Dictionary<Guid, int>> GetAvailableAsync(
        IUnitOfWork uow, Guid roomId, IReadOnlyCollection<Guid>? componentIds, CancellationToken ct)
    {
        var stocks = uow.Repository<ConsumableStock>().QueryNoTracking()
            .Where(s => s.Cabinet.RoomId == roomId);
        var units = uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.Cabinet.RoomId == roomId && i.Status == ComponentItemStatuses.Available);
        var reservations = uow.Repository<BorrowItem>().QueryNoTracking()
            .Where(i => i.BorrowRequest.RoomId == roomId && i.BorrowRequest.Status == BorrowRequestStatuses.Approved);

        if (componentIds != null)
        {
            stocks = stocks.Where(s => componentIds.Contains(s.ComponentId));
            units = units.Where(i => componentIds.Contains(i.ComponentId));
            reservations = reservations.Where(i => componentIds.Contains(i.ComponentId));
        }

        var consumable = await stocks
            .GroupBy(s => s.ComponentId)
            .Select(g => new { ComponentId = g.Key, Quantity = g.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => x.ComponentId, x => x.Quantity, ct);

        var unitCounts = await units
            .GroupBy(i => i.ComponentId)
            .Select(g => new { ComponentId = g.Key, Quantity = g.Count() })
            .ToDictionaryAsync(x => x.ComponentId, x => x.Quantity, ct);

        var reserved = await reservations
            .GroupBy(i => i.ComponentId)
            .Select(g => new { ComponentId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.ComponentId, x => x.Quantity, ct);

        var available = new Dictionary<Guid, int>();
        foreach (var componentId in consumable.Keys.Union(unitCounts.Keys))
        {
            var quantity = consumable.GetValueOrDefault(componentId)
                           + unitCounts.GetValueOrDefault(componentId)
                           - reserved.GetValueOrDefault(componentId);
            if (quantity > 0)
                available[componentId] = quantity;
        }

        return available;
    }

    /// <summary>
    /// Locks the lab row (no-op UPDATE). Approving a request and taking stock out of a lab both take this lock,
    /// so they run one after another and cannot both use the same free units.
    /// </summary>
    public static Task LockRoomAsync(IUnitOfWork uow, Guid roomId, CancellationToken ct)
        => uow.Repository<LabRoom>().Query()
            .Where(r => r.RoomId == roomId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, r => r.Status), ct);

    public static async Task<bool> SameRoomAsync(IUnitOfWork uow, Guid cabinetId, Guid otherCabinetId, CancellationToken ct)
    {
        if (cabinetId == otherCabinetId)
            return true;

        var rooms = await uow.Repository<StorageCabinet>().QueryNoTracking()
            .Where(c => c.CabinetId == cabinetId || c.CabinetId == otherCabinetId)
            .Select(c => c.RoomId)
            .Distinct()
            .CountAsync(ct);
        return rooms == 1;
    }

    /// <summary>
    /// Stock removed from a lab by a staff operation (cancel a receipt, move to another lab, move out of Available)
    /// may only come from the part not reserved by Approved requests; otherwise 409. Must run inside the caller's
    /// transaction, before the removal. Counted stock and broken units are not checked here: they are facts.
    /// </summary>
    public static async Task EnsureNotReservedAsync(IUnitOfWork uow, IReadOnlyList<StockRemoval> removals, CancellationToken ct)
    {
        if (removals.Count == 0)
            return;

        var cabinetIds = removals.Select(r => r.CabinetId).Distinct().ToList();
        var roomOfCabinet = await uow.Repository<StorageCabinet>().QueryNoTracking()
            .Where(c => cabinetIds.Contains(c.CabinetId))
            .ToDictionaryAsync(c => c.CabinetId, c => c.RoomId, ct);

        var shortages = new List<(Guid ComponentId, int Taken, int Free)>();
        foreach (var room in removals.GroupBy(r => roomOfCabinet[r.CabinetId]).OrderBy(g => g.Key))
        {
            await LockRoomAsync(uow, room.Key, ct);

            var taken = room.GroupBy(r => r.ComponentId).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));
            var free = await GetAvailableAsync(uow, room.Key, taken.Keys.ToList(), ct);
            foreach (var (componentId, quantity) in taken)
            {
                var left = free.GetValueOrDefault(componentId);
                if (left < quantity)
                    shortages.Add((componentId, quantity, left));
            }
        }

        if (shortages.Count == 0)
            return;

        var ids = shortages.Select(s => s.ComponentId).ToList();
        var codes = await uow.Repository<Component>().QueryNoTracking()
            .Where(c => ids.Contains(c.ComponentId))
            .ToDictionaryAsync(c => c.ComponentId, c => c.ComponentCode, ct);
        var details = shortages
            .Select(s => $"{codes[s.ComponentId]}: lấy {s.Taken}, chỉ còn {s.Free} chưa giữ chỗ")
            .ToArray();

        throw new ConflictException(
            "Không lấy được hàng đang được giữ chỗ cho đơn mượn đã duyệt (chưa cấp đồ)",
            new Dictionary<string, string[]> { ["items"] = details });
    }
}

using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
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
        var inStock = await GetInStockAsync(uow, roomId, componentIds, ct);
        var reserved = await GetReservedAsync(uow, roomId, componentIds, ct);

        var available = new Dictionary<Guid, int>();
        foreach (var (componentId, stock) in inStock)
        {
            var quantity = stock - reserved.GetValueOrDefault(componentId);
            if (quantity > 0)
                available[componentId] = quantity;
        }

        return available;
    }

    /// <summary>
    /// Components (of the given ones) whose stock in the lab is now below what Approved requests of the lab reserve,
    /// with the affected request codes. Called after a staff operation records broken or lost stock, which is allowed
    /// even when the stock was reserved, so the response can warn the staff.
    /// </summary>
    public static async Task<List<ReservationShortageResponse>> GetShortagesAsync(
        IUnitOfWork uow, Guid roomId, IReadOnlyCollection<Guid> componentIds, CancellationToken ct)
    {
        if (componentIds.Count == 0)
            return new List<ReservationShortageResponse>();

        var reserved = await GetReservedAsync(uow, roomId, componentIds, ct);
        if (reserved.Count == 0)
            return new List<ReservationShortageResponse>();

        var inStock = await GetInStockAsync(uow, roomId, reserved.Keys.ToList(), ct);
        var shortIds = reserved
            .Where(r => inStock.GetValueOrDefault(r.Key) < r.Value)
            .Select(r => r.Key)
            .ToList();
        if (shortIds.Count == 0)
            return new List<ReservationShortageResponse>();

        var roomCode = await uow.Repository<LabRoom>().QueryNoTracking()
            .Where(r => r.RoomId == roomId)
            .Select(r => r.RoomCode)
            .FirstAsync(ct);

        var requests = await uow.Repository<BorrowItem>().QueryNoTracking()
            .Where(i => shortIds.Contains(i.ComponentId)
                        && i.BorrowRequest.RoomId == roomId
                        && i.BorrowRequest.Status == BorrowRequestStatuses.Approved)
            .Select(i => new { i.ComponentId, i.BorrowRequest.RequestCode })
            .Distinct()
            .ToListAsync(ct);

        var components = await uow.Repository<Component>().QueryNoTracking()
            .Where(c => shortIds.Contains(c.ComponentId))
            .OrderBy(c => c.ComponentCode)
            .Select(c => new { c.ComponentId, c.ComponentCode, c.ComponentName })
            .ToListAsync(ct);

        var shortages = new List<ReservationShortageResponse>();
        foreach (var component in components)
        {
            shortages.Add(new ReservationShortageResponse
            {
                ComponentId = component.ComponentId,
                ComponentCode = component.ComponentCode,
                ComponentName = component.ComponentName,
                RoomId = roomId,
                RoomCode = roomCode,
                InStock = inStock.GetValueOrDefault(component.ComponentId),
                Reserved = reserved[component.ComponentId],
                RequestCodes = requests
                    .Where(r => r.ComponentId == component.ComponentId)
                    .Select(r => r.RequestCode)
                    .OrderBy(code => code)
                    .ToList(),
            });
        }

        return shortages;
    }

    public static Task<Guid> GetRoomIdAsync(IUnitOfWork uow, Guid cabinetId, CancellationToken ct)
        => uow.Repository<StorageCabinet>().QueryNoTracking()
            .Where(c => c.CabinetId == cabinetId)
            .Select(c => c.RoomId)
            .FirstAsync(ct);

    /// <summary>Lendable stock per component in the lab: CONSUMABLE quantity + Available units.</summary>
    private static async Task<Dictionary<Guid, int>> GetInStockAsync(
        IUnitOfWork uow, Guid roomId, IReadOnlyCollection<Guid>? componentIds, CancellationToken ct)
    {
        var stocks = uow.Repository<ConsumableStock>().QueryNoTracking()
            .Where(s => s.Cabinet.RoomId == roomId);
        var units = uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.Cabinet.RoomId == roomId && i.Status == ComponentItemStatuses.Available);

        if (componentIds != null)
        {
            stocks = stocks.Where(s => componentIds.Contains(s.ComponentId));
            units = units.Where(i => componentIds.Contains(i.ComponentId));
        }

        var consumable = await stocks
            .GroupBy(s => s.ComponentId)
            .Select(g => new { ComponentId = g.Key, Quantity = g.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => x.ComponentId, x => x.Quantity, ct);

        var unitCounts = await units
            .GroupBy(i => i.ComponentId)
            .Select(g => new { ComponentId = g.Key, Quantity = g.Count() })
            .ToDictionaryAsync(x => x.ComponentId, x => x.Quantity, ct);

        var inStock = new Dictionary<Guid, int>();
        foreach (var componentId in consumable.Keys.Union(unitCounts.Keys))
            inStock[componentId] = consumable.GetValueOrDefault(componentId) + unitCounts.GetValueOrDefault(componentId);

        return inStock;
    }

    /// <summary>Quantity per component reserved by Approved (not issued yet) requests of the lab.</summary>
    private static async Task<Dictionary<Guid, int>> GetReservedAsync(
        IUnitOfWork uow, Guid roomId, IReadOnlyCollection<Guid>? componentIds, CancellationToken ct)
    {
        var reservations = uow.Repository<BorrowItem>().QueryNoTracking()
            .Where(i => i.BorrowRequest.RoomId == roomId && i.BorrowRequest.Status == BorrowRequestStatuses.Approved);

        if (componentIds != null)
            reservations = reservations.Where(i => componentIds.Contains(i.ComponentId));

        return await reservations
            .GroupBy(i => i.ComponentId)
            .Select(g => new { ComponentId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.ComponentId, x => x.Quantity, ct);
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
    /// Stock removed from a lab by a staff decision (cancel a receipt, move to another lab, send to Maintenance,
    /// retire) may only come from the part not reserved by Approved requests; otherwise 409. Must run inside the caller's
    /// transaction, before the removal. Facts (counted stock, Broken, Lost) are never blocked: callers report them
    /// with <see cref="GetShortagesAsync"/> instead.
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

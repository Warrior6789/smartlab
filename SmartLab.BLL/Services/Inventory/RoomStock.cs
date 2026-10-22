using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

/// <summary>
/// What a lab can still lend: stock in the lab's cabinets (CONSUMABLE quantity + Available units) minus what borrow
/// requests reserve there: every line of an Approved or Ready request, and a replacement line of a request being borrowed
/// (all of them still Pending, not issued yet). A reserved line holds stock in its source lab until the pickup lab receives
/// it, then in the pickup lab. Used when submitting, approving and suggesting, so "còn" means the same everywhere.
/// </summary>
internal static class RoomStock
{
    /// <summary>Borrow request statuses whose lines hold stock (nothing handed over yet).</summary>
    public static readonly string[] ReservingStatuses = { BorrowRequestStatuses.Approved, BorrowRequestStatuses.Ready };

    /// <summary>Borrow request statuses whose Pending lines hold stock: <see cref="ReservingStatuses"/> plus replacements.</summary>
    private static readonly string[] HoldingStatuses =
    {
        BorrowRequestStatuses.Approved, BorrowRequestStatuses.Ready,
        BorrowRequestStatuses.Borrowing, BorrowRequestStatuses.Overdue,
    };

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

    /// <summary>Available quantity per lab, then per component (never negative); empty entries are absent.</summary>
    public static async Task<Dictionary<Guid, Dictionary<Guid, int>>> GetAvailableByRoomAsync(
        IUnitOfWork uow, IReadOnlyCollection<Guid>? componentIds, CancellationToken ct)
    {
        var inStock = await GetInStockRowsAsync(uow, null, componentIds, ct);
        var reserved = await GetReservedRowsAsync(uow, componentIds, ct);

        var byRoom = new Dictionary<Guid, Dictionary<Guid, int>>();
        foreach (var row in inStock)
        {
            var quantity = row.Quantity;
            foreach (var hold in reserved.Where(r => r.RoomId == row.RoomId && r.ComponentId == row.ComponentId))
                quantity -= hold.Quantity;

            if (quantity <= 0)
                continue;

            if (!byRoom.ContainsKey(row.RoomId))
                byRoom[row.RoomId] = new Dictionary<Guid, int>();
            byRoom[row.RoomId][row.ComponentId] = quantity;
        }

        return byRoom;
    }

    /// <summary>Available quantity per component summed over all labs; components with nothing left are absent.</summary>
    public static async Task<Dictionary<Guid, int>> GetTotalAvailableAsync(
        IUnitOfWork uow, IReadOnlyCollection<Guid>? componentIds, CancellationToken ct)
    {
        var byRoom = await GetAvailableByRoomAsync(uow, componentIds, ct);

        var total = new Dictionary<Guid, int>();
        foreach (var room in byRoom.Values)
        {
            foreach (var (componentId, quantity) in room)
                total[componentId] = total.GetValueOrDefault(componentId) + quantity;
        }

        return total;
    }

    /// <summary>
    /// Components (of the given ones) whose stock in the lab is now below what borrow requests reserve there,
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
                        && i.Status == BorrowItemStatuses.Pending
                        && HoldingStatuses.Contains(i.BorrowRequest.Status)
                        && ((i.ReceivedAt == null && i.SourceRoomId == roomId)
                            || (i.ReceivedAt != null && i.BorrowRequest.RoomId == roomId)))
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

    /// <summary>Lendable stock per component in the lab: CONSUMABLE quantity + Available units without an Open issue report.</summary>
    private static async Task<Dictionary<Guid, int>> GetInStockAsync(
        IUnitOfWork uow, Guid roomId, IReadOnlyCollection<Guid>? componentIds, CancellationToken ct)
    {
        var rows = await GetInStockRowsAsync(uow, roomId, componentIds, ct);

        var inStock = new Dictionary<Guid, int>();
        foreach (var row in rows)
            inStock[row.ComponentId] = inStock.GetValueOrDefault(row.ComponentId) + row.Quantity;

        return inStock;
    }

    private sealed record RoomQuantity(Guid RoomId, Guid ComponentId, int Quantity);

    /// <summary>Lendable stock per lab and component (one lab when <paramref name="roomId"/> is given).</summary>
    private static async Task<List<RoomQuantity>> GetInStockRowsAsync(
        IUnitOfWork uow, Guid? roomId, IReadOnlyCollection<Guid>? componentIds, CancellationToken ct)
    {
        var stocks = uow.Repository<ConsumableStock>().QueryNoTracking()
            .Where(s => s.Quantity > 0);
        var units = uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.Status == ComponentItemStatuses.Available
                        && !i.IssueReports.Any(r => r.Status == IssueReportStatuses.Open));

        if (roomId != null)
        {
            stocks = stocks.Where(s => s.Cabinet.RoomId == roomId);
            units = units.Where(i => i.Cabinet.RoomId == roomId);
        }

        if (componentIds != null)
        {
            stocks = stocks.Where(s => componentIds.Contains(s.ComponentId));
            units = units.Where(i => componentIds.Contains(i.ComponentId));
        }

        var consumable = await stocks
            .GroupBy(s => new { s.Cabinet.RoomId, s.ComponentId })
            .Select(g => new { g.Key.RoomId, g.Key.ComponentId, Quantity = g.Sum(s => s.Quantity) })
            .ToListAsync(ct);

        var unitCounts = await units
            .GroupBy(i => new { i.Cabinet.RoomId, i.ComponentId })
            .Select(g => new { g.Key.RoomId, g.Key.ComponentId, Quantity = g.Count() })
            .ToListAsync(ct);

        var rows = new List<RoomQuantity>();
        foreach (var row in consumable)
            rows.Add(new RoomQuantity(row.RoomId, row.ComponentId, row.Quantity));
        foreach (var row in unitCounts)
            rows.Add(new RoomQuantity(row.RoomId, row.ComponentId, row.Quantity));

        return rows;
    }

    /// <summary>Quantity per component reserved in the lab by Pending lines (see the class summary).</summary>
    private static async Task<Dictionary<Guid, int>> GetReservedAsync(
        IUnitOfWork uow, Guid roomId, IReadOnlyCollection<Guid>? componentIds, CancellationToken ct)
    {
        var reservations = uow.Repository<BorrowItem>().QueryNoTracking()
            .Where(i => i.Status == BorrowItemStatuses.Pending
                        && HoldingStatuses.Contains(i.BorrowRequest.Status)
                        && ((i.ReceivedAt == null && i.SourceRoomId == roomId)
                            || (i.ReceivedAt != null && i.BorrowRequest.RoomId == roomId)));

        if (componentIds != null)
            reservations = reservations.Where(i => componentIds.Contains(i.ComponentId));

        return await reservations
            .GroupBy(i => i.ComponentId)
            .Select(g => new { ComponentId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.ComponentId, x => x.Quantity, ct);
    }

    /// <summary>
    /// Reserved quantity per lab and component: lines not received yet count in their source lab, received lines in the
    /// request's pickup lab.
    /// </summary>
    private static async Task<List<RoomQuantity>> GetReservedRowsAsync(
        IUnitOfWork uow, IReadOnlyCollection<Guid>? componentIds, CancellationToken ct)
    {
        var lines = uow.Repository<BorrowItem>().QueryNoTracking()
            .Where(i => i.Status == BorrowItemStatuses.Pending && HoldingStatuses.Contains(i.BorrowRequest.Status));

        if (componentIds != null)
            lines = lines.Where(i => componentIds.Contains(i.ComponentId));

        var waiting = await lines
            .Where(i => i.ReceivedAt == null && i.SourceRoomId != null)
            .GroupBy(i => new { RoomId = i.SourceRoomId!.Value, i.ComponentId })
            .Select(g => new { g.Key.RoomId, g.Key.ComponentId, Quantity = g.Sum(i => i.Quantity) })
            .ToListAsync(ct);

        var received = await lines
            .Where(i => i.ReceivedAt != null)
            .GroupBy(i => new { i.BorrowRequest.RoomId, i.ComponentId })
            .Select(g => new { g.Key.RoomId, g.Key.ComponentId, Quantity = g.Sum(i => i.Quantity) })
            .ToListAsync(ct);

        var rows = new List<RoomQuantity>();
        foreach (var row in waiting)
            rows.Add(new RoomQuantity(row.RoomId, row.ComponentId, row.Quantity));
        foreach (var row in received)
            rows.Add(new RoomQuantity(row.RoomId, row.ComponentId, row.Quantity));

        return rows;
    }

    /// <summary>
    /// Locks the lab row (no-op UPDATE). Approving a request and taking stock out of a lab both take this lock,
    /// so they run one after another and cannot both use the same free units.
    /// </summary>
    public static Task LockRoomAsync(IUnitOfWork uow, Guid roomId, CancellationToken ct)
        => uow.Repository<LabRoom>().Query()
            .Where(r => r.RoomId == roomId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, r => r.Status), ct);

    /// <summary>Locks several labs one by one in the same order as <see cref="EnsureNotReservedAsync"/>, so they cannot deadlock.</summary>
    public static async Task LockRoomsAsync(IUnitOfWork uow, IEnumerable<Guid> roomIds, CancellationToken ct)
    {
        foreach (var roomId in roomIds.Distinct().OrderBy(id => id))
            await LockRoomAsync(uow, roomId, ct);
    }

    /// <summary>Locks every lab: approving may take stock from any of them.</summary>
    public static async Task LockAllRoomsAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var roomIds = await uow.Repository<LabRoom>().QueryNoTracking()
            .Select(r => r.RoomId)
            .ToListAsync(ct);
        await LockRoomsAsync(uow, roomIds, ct);
    }

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
    /// Stock removed from a lab by a staff decision (cancel a receipt, move to another lab, retire) may only come from the part not reserved by borrow requests; otherwise 409. Must run inside the caller's
    /// transaction, before the removal. Facts (counted stock, Lost) are never blocked: callers report them
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
            "Không lấy được hàng đang được giữ chỗ cho đơn mượn (chưa giao đồ)",
            new Dictionary<string, string[]> { ["items"] = details });
    }
}

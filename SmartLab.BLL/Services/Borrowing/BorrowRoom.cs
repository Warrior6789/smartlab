using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.Services.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Chooses the lab a borrow request belongs to: the lab where students pick up and return the components.
/// Students do not choose it. What that lab lacks is transferred from the other labs after approval.
/// </summary>
internal static class BorrowRoom
{
    /// <summary>Labs with an active Lab Staff, who are told to carry goods over.</summary>
    public static async Task<HashSet<Guid>> GetStaffedIdsAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var ids = await uow.Repository<LabRoomStaff>().QueryNoTracking()
            .Where(s => s.User.IsActive)
            .Select(s => s.RoomId)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    /// <summary>The best lab for these lines, with stock read now. See the overload for how it is chosen.</summary>
    public static async Task<Guid> PickBestAsync(
        IUnitOfWork uow, IReadOnlyCollection<(Guid ComponentId, int Quantity)> lines, CancellationToken ct)
    {
        var ids = lines.Select(l => l.ComponentId).Distinct().ToList();
        var available = await RoomStock.GetAvailableByRoomAsync(uow, ids, ct);
        return await PickBestAsync(uow, lines, available, ct);
    }

    /// <summary>
    /// Only active labs with an active Lab Staff can hand over components. Among them: the lab that can fill the most lines
    /// on its own, then the one that can give the most of what the lines need, then by lab code so the result is stable.
    /// 400 when no lab can take borrow requests.
    /// </summary>
    public static async Task<Guid> PickBestAsync(
        IUnitOfWork uow, IReadOnlyCollection<(Guid ComponentId, int Quantity)> lines,
        IReadOnlyDictionary<Guid, Dictionary<Guid, int>> availableByRoom, CancellationToken ct)
    {
        var rooms = await uow.Repository<LabRoom>().QueryNoTracking()
            .Where(r => r.Status == LabRoomStatuses.Active && r.LabRoomStaffs.Any(s => s.User.IsActive))
            .Select(r => new { r.RoomId, r.RoomCode })
            .ToListAsync(ct);
        if (rooms.Count == 0)
            throw new BadRequestException("Chưa có lab nào đang hoạt động và có Lab Staff, vui lòng báo Admin");

        var needed = lines.GroupBy(l => l.ComponentId).Select(g => (ComponentId: g.Key, Quantity: g.Sum(l => l.Quantity))).ToList();

        return rooms
            .Select(room =>
            {
                var stock = availableByRoom.GetValueOrDefault(room.RoomId);
                var have = needed.Select(n => (n.Quantity, Left: stock?.GetValueOrDefault(n.ComponentId) ?? 0)).ToList();
                return new
                {
                    room.RoomId,
                    room.RoomCode,
                    FullLines = have.Count(h => h.Left >= h.Quantity),
                    Quantity = have.Sum(h => Math.Min(h.Left, h.Quantity)),
                };
            })
            .OrderByDescending(r => r.FullLines)
            .ThenByDescending(r => r.Quantity)
            .ThenBy(r => r.RoomCode, StringComparer.Ordinal)
            .First()
            .RoomId;
    }
}

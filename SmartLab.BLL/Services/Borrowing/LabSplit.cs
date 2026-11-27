using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.Services.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Splits what a project borrows into one request per lab: each component goes to a lab that keeps it (lab_room_components).
/// A component kept by several labs (resistors, wires...) joins the lab that already serves the most lines, among the labs
/// that have enough of it, so the team gets as few requests as possible. One component is never split across labs, and labs
/// never pass components to each other. Only active labs with an active Lab Staff take requests.
/// </summary>
internal static class LabSplit
{
    public sealed record Line(Guid ComponentId, string ComponentName, int Quantity);

    public sealed record LabLines(Guid RoomId, string RoomCode, List<Line> Lines);

    public static async Task<List<LabLines>> SplitAsync(IUnitOfWork uow, IReadOnlyList<Line> lines, CancellationToken ct)
    {
        var ids = lines.Select(l => l.ComponentId).Distinct().ToList();
        var keepers = await uow.Repository<LabRoomComponent>().QueryNoTracking()
            .Where(l => ids.Contains(l.ComponentId)
                        && l.Room.Status == LabRoomStatuses.Active
                        && l.Room.LabRoomStaffs.Any(s => s.User.IsActive))
            .Select(l => new { l.ComponentId, l.RoomId, l.Room.RoomCode })
            .ToListAsync(ct);

        var labsByComponent = keepers.GroupBy(k => k.ComponentId).ToDictionary(g => g.Key, g => g.Select(k => k.RoomId).ToList());
        var roomCodes = keepers.GroupBy(k => k.RoomId).ToDictionary(g => g.Key, g => g.First().RoomCode);

        var homeless = lines.Where(l => !labsByComponent.ContainsKey(l.ComponentId)).Select(l => l.ComponentName).Distinct().ToList();
        if (homeless.Count > 0)
            throw new BadRequestException(
                $"Chưa có lab đang hoạt động nào giữ {string.Join(", ", homeless)}. Hãy ghi vào mục tự chuẩn bị");

        var available = await RoomStock.GetAvailableByRoomAsync(uow, ids, ct);
        int Left(Guid roomId, Guid componentId) => available.GetValueOrDefault(roomId)?.GetValueOrDefault(componentId) ?? 0;

        var assigned = new Dictionary<Guid, List<Line>>();
        void Assign(Guid roomId, Line line)
        {
            if (!assigned.TryGetValue(roomId, out var list))
            {
                list = new List<Line>();
                assigned[roomId] = list;
            }
            list.Add(line);
        }

        foreach (var line in lines.Where(l => labsByComponent[l.ComponentId].Count == 1))
            Assign(labsByComponent[line.ComponentId][0], line);

        foreach (var line in lines.Where(l => labsByComponent[l.ComponentId].Count > 1).OrderBy(l => l.ComponentName))
        {
            var roomId = labsByComponent[line.ComponentId]
                .OrderByDescending(r => Left(r, line.ComponentId) >= line.Quantity)
                .ThenByDescending(r => assigned.GetValueOrDefault(r)?.Count ?? 0)
                .ThenByDescending(r => Left(r, line.ComponentId))
                .ThenBy(r => roomCodes[r], StringComparer.Ordinal)
                .First();
            Assign(roomId, line);
        }

        var shortages = new List<string>();
        foreach (var (roomId, roomLines) in assigned)
        {
            foreach (var line in roomLines)
            {
                var left = Left(roomId, line.ComponentId);
                if (left < line.Quantity)
                    shortages.Add($"{line.ComponentName} (cần {line.Quantity}, {roomCodes[roomId]} còn {left})");
            }
        }
        if (shortages.Count > 0)
            throw new BadRequestException(
                $"Không đủ linh kiện: {string.Join("; ", shortages)}. " +
                "Các lab không chuyển đồ cho nhau, hãy giảm số lượng hoặc đưa món thiếu vào danh sách tự mua.");

        return assigned
            .Select(a => new LabLines(a.Key, roomCodes[a.Key], a.Value))
            .OrderBy(l => l.RoomCode, StringComparer.Ordinal)
            .ToList();
    }
}

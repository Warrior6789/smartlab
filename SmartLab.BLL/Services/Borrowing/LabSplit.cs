using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.Services.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Splits what a project borrows into one request per lab: each component goes to a lab that has it in stock now.
/// A component several labs hold (resistors, wires...) joins the lab that already serves the most lines, among the labs
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
        var roomCodes = await uow.Repository<LabRoom>().QueryNoTracking()
            .Where(r => r.Status == LabRoomStatuses.Active && r.LabRoomStaffs.Any(s => s.User.IsActive))
            .ToDictionaryAsync(r => r.RoomId, r => r.RoomCode, ct);

        var available = (await RoomStock.GetAvailableByRoomAsync(uow, ids, ct))
            .Where(r => roomCodes.ContainsKey(r.Key))
            .ToDictionary(r => r.Key, r => r.Value);
        int Left(Guid roomId, Guid componentId) => available.GetValueOrDefault(roomId)?.GetValueOrDefault(componentId) ?? 0;

        var labsByComponent = new Dictionary<Guid, List<Guid>>();
        foreach (var id in ids)
        {
            var holders = available.Keys.Where(roomId => Left(roomId, id) > 0).ToList();
            if (holders.Count > 0)
                labsByComponent[id] = holders;
        }

        var missing = lines.Where(l => !labsByComponent.ContainsKey(l.ComponentId)).Select(l => l.ComponentName).Distinct().ToList();
        if (missing.Count > 0)
            throw new BadRequestException(
                $"Không lab nào còn {string.Join(", ", missing)}. Hãy giảm bớt hoặc ghi vào mục tự chuẩn bị");

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

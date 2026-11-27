using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.Services.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// A project borrows from one lab only: the lab of its class's course (courses.room_id). Every component must be on that
/// lab's list (lab_room_components) and in stock there; labs never pass components to each other.
/// </summary>
internal static class LabSplit
{
    private const int MaxNamesShown = 5;

    public sealed record Line(Guid ComponentId, string ComponentName, int Quantity);

    public sealed record LabLines(Guid RoomId, string RoomCode, List<Line> Lines);

    public sealed record ClassLab(Guid RoomId, string RoomCode);

    /// <summary>The lab of the class's course; 400 when the course has no lab or the lab cannot take requests.</summary>
    public static async Task<ClassLab> GetClassLabAsync(IUnitOfWork uow, Guid? classId, CancellationToken ct)
    {
        if (classId == null)
            throw new BadRequestException("Project chưa thuộc lớp nào nên chưa mượn được");

        var info = await uow.Repository<Class>().QueryNoTracking()
            .Where(c => c.ClassId == classId)
            .Select(c => new
            {
                c.Course.CourseCode,
                c.Course.RoomId,
                RoomCode = c.Course.Room != null ? c.Course.Room.RoomCode : null,
                Active = c.Course.Room != null && c.Course.Room.Status == LabRoomStatuses.Active,
                HasStaff = c.Course.Room != null && c.Course.Room.LabRoomStaffs.Any(s => s.User.IsActive),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy lớp");

        if (info.RoomId == null || info.RoomCode == null)
            throw new BadRequestException($"Môn {info.CourseCode} chưa gắn lab nên chưa mượn được. Nhờ quản trị chọn lab cho môn");
        if (!info.Active)
            throw new BadRequestException($"{info.RoomCode} đang tạm ngưng nên chưa mượn được");
        if (!info.HasStaff)
            throw new BadRequestException($"{info.RoomCode} chưa có nhân viên phụ trách nên chưa mượn được");

        return new ClassLab(info.RoomId.Value, info.RoomCode);
    }

    /// <summary>400 when some components are not on the class lab's list; those go to the self-supplied list.</summary>
    public static async Task EnsureOnClassLabListAsync(
        IUnitOfWork uow, Guid? classId, IReadOnlyCollection<Guid> componentIds, CancellationToken ct)
    {
        if (componentIds.Count == 0)
            return;

        var lab = await GetClassLabAsync(uow, classId, ct);
        var missing = await uow.Repository<Component>().QueryNoTracking()
            .Where(c => componentIds.Contains(c.ComponentId) && !c.LabRoomComponents.Any(l => l.RoomId == lab.RoomId))
            .OrderBy(c => c.ComponentCode)
            .Select(c => c.ComponentName)
            .ToListAsync(ct);
        if (missing.Count > 0)
            throw new BadRequestException($"{lab.RoomCode} không có {JoinNames(missing)}. Hãy bỏ ra hoặc ghi vào mục tự chuẩn bị");
    }

    /// <summary>All lines go to the class's lab as one request; 400 when a component is not on its list or short there.</summary>
    public static async Task<List<LabLines>> SplitAsync(IUnitOfWork uow, Guid? classId, IReadOnlyList<Line> lines, CancellationToken ct)
    {
        var ids = lines.Select(l => l.ComponentId).Distinct().ToList();
        await EnsureOnClassLabListAsync(uow, classId, ids, ct);
        var lab = await GetClassLabAsync(uow, classId, ct);

        var available = await RoomStock.GetAvailableByRoomAsync(uow, ids, ct);
        var inLab = available.GetValueOrDefault(lab.RoomId);

        var shortages = new List<string>();
        foreach (var line in lines)
        {
            var left = inLab?.GetValueOrDefault(line.ComponentId) ?? 0;
            if (left < line.Quantity)
                shortages.Add($"{line.ComponentName} (cần {line.Quantity}, {lab.RoomCode} còn {left})");
        }
        if (shortages.Count > 0)
            throw new BadRequestException(
                $"Không đủ linh kiện: {string.Join("; ", shortages)}. " +
                "Các lab không chuyển đồ cho nhau, hãy giảm số lượng hoặc đưa món thiếu vào danh sách tự mua.");

        return new List<LabLines> { new(lab.RoomId, lab.RoomCode, lines.ToList()) };
    }

    private static string JoinNames(List<string> names)
    {
        if (names.Count <= MaxNamesShown)
            return string.Join(", ", names);
        return $"{string.Join(", ", names.Take(MaxNamesShown))} và {names.Count - MaxNamesShown} linh kiện khác";
    }
}

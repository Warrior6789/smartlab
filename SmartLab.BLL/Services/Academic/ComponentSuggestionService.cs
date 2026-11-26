using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.External.Ai;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Services.Borrowing;
using SmartLab.BLL.Services.Inventory;
using SmartLab.BLL.Validators.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

/// <summary>
/// The AI only says what the project needs. Whether the lab has it is always read from the database:
/// unknown codes are dropped and stock never comes from the AI. A project borrows everything from one lab: the lab of
/// its course when the class is known (see CourseRoom), otherwise the one with the most of what it needs (see BorrowRoom).
/// The groups are split by that lab's stock: a catalog component that lab lacks goes to the self-supplied list, even
/// when another lab has it. The AI only suggests; nothing is saved until the team saves the project.
/// </summary>
public class ComponentSuggestionService : IComponentSuggestionService
{
    private const int ReasonMaxLength = 300;

    private const string EmptyHint =
        "AI chưa gợi ý được linh kiện nào. Hãy mô tả rõ hơn project làm gì, đo hoặc phát hiện gì, điều khiển thiết bị nào " +
        "và dùng bo mạch gì. Project chỉ có phần mềm hoặc không phù hợp quy định thì sẽ không có gợi ý.";

    private readonly IUnitOfWork _uow;
    private readonly IAiComponentAdvisor _advisor;

    public ComponentSuggestionService(IUnitOfWork uow, IAiComponentAdvisor advisor)
    {
        _uow = uow;
        _advisor = advisor;
    }

    public async Task<ComponentSuggestionResponse> SuggestAsync(SuggestComponentsRequest request, CancellationToken ct = default)
    {
        var availableByRoom = await RoomStock.GetAvailableByRoomAsync(_uow, null, ct);

        Guid? courseRoomId = null;
        if (request.ClassId != null)
        {
            courseRoomId = await CourseRoom.GetForClassAsync(_uow, request.ClassId, ct);
            availableByRoom = new Dictionary<Guid, Dictionary<Guid, int>>
            {
                [courseRoomId.Value] = availableByRoom.GetValueOrDefault(courseRoomId.Value) ?? new Dictionary<Guid, int>(),
            };
        }

        var catalog = await LoadCatalogAsync(availableByRoom, ct);

        var aiCatalog = catalog.Select(c => new AiCatalogItem
        {
            ComponentCode = c.ComponentCode,
            ComponentName = c.ComponentName,
            CategoryName = c.CategoryName,
            Description = c.Description,
            Unit = c.Unit,
            Available = c.Available,
        }).Where(c => courseRoomId == null || c.Available > 0).ToList();

        var advice = await _advisor.AdviseAsync(request.Description.Trim(), aiCatalog, ct);

        var byCode = catalog.ToDictionary(c => c.ComponentCode.ToUpper());
        var byName = new Dictionary<string, CatalogRow>();
        foreach (var row in catalog)
            byName.TryAdd(row.ComponentName.Trim().ToLower(), row);

        var result = new ComponentSuggestionResponse();
        var picks = new List<CatalogPick>();
        var pickedIds = new HashSet<Guid>();
        var addedSelfSupplied = new HashSet<string>();

        foreach (var item in advice.InStock)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ComponentCode))
                continue;

            if (byCode.TryGetValue(item.ComponentCode.Trim().ToUpper(), out var row) && pickedIds.Add(row.ComponentId))
                picks.Add(new CatalogPick(row, ClampQuantity(item.Quantity), CleanReason(item.Reason)));
        }

        foreach (var item in advice.SelfSupplied)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemName))
                continue;

            var name = item.ItemName.Trim();
            if (byName.TryGetValue(name.ToLower(), out var row))
            {
                if (pickedIds.Add(row.ComponentId))
                    picks.Add(new CatalogPick(row, ClampQuantity(item.Quantity), CleanReason(item.Reason)));
                continue;
            }

            AddSelfSupplied(result, addedSelfSupplied, name, item.Quantity, item.Reason);
        }

        var lines = picks.Select(p => (p.Row.ComponentId, p.Quantity)).ToList();
        var roomId = courseRoomId;
        if (roomId == null && lines.Count > 0)
            roomId = await BorrowRoom.PickBestOrNullAsync(_uow, lines, availableByRoom, ct);

        var labStock = new Dictionary<Guid, int>();
        if (roomId != null)
        {
            labStock = availableByRoom.GetValueOrDefault(roomId.Value) ?? new Dictionary<Guid, int>();
            result.Lab = await _uow.Repository<LabRoom>().QueryNoTracking()
                .Where(r => r.RoomId == roomId.Value)
                .Select(r => new SuggestedLabResponse { RoomId = r.RoomId, RoomCode = r.RoomCode, RoomName = r.RoomName })
                .FirstAsync(ct);
        }

        foreach (var pick in picks)
        {
            var inLab = labStock.GetValueOrDefault(pick.Row.ComponentId);
            if (inLab >= pick.Quantity)
                result.Borrowable.Add(ToSuggestion(pick));
            else if (inLab > 0)
                result.NotEnoughStock.Add(ToNotEnough(pick, inLab));
            else
                AddSelfSupplied(result, addedSelfSupplied, pick.Row.ComponentName, pick.Quantity, pick.Reason);
        }

        if (result.Borrowable.Count == 0 && result.NotEnoughStock.Count == 0 && result.SelfSupplied.Count == 0)
            result.Hint = EmptyHint;

        return result;
    }

    private sealed class CatalogRow
    {
        public Guid ComponentId { get; init; }
        public string ComponentCode { get; init; } = string.Empty;

        /// <summary>Code shown to users; the AI works with ComponentCode.</summary>
        public string DisplayCode { get; init; } = string.Empty;
        public string ComponentName { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string TrackingType { get; init; } = string.Empty;
        public string Unit { get; init; } = string.Empty;
        public int Available { get; set; }
    }

    private sealed record CatalogPick(CatalogRow Row, int Quantity, string Reason);

    /// <summary>
    /// Active components with the most one lab can still lend (stock minus approved reservations, see
    /// <see cref="RoomStock"/>), because a project borrows from a single lab.
    /// </summary>
    private async Task<List<CatalogRow>> LoadCatalogAsync(
        Dictionary<Guid, Dictionary<Guid, int>> availableByRoom, CancellationToken ct)
    {
        var rows = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.ComponentCode)
            .Select(c => new CatalogRow
            {
                ComponentId = c.ComponentId,
                ComponentCode = c.ComponentCode,
                DisplayCode = c.ShortCode ?? c.ComponentCode,
                ComponentName = c.ComponentName,
                CategoryName = c.Category.CategoryName,
                Description = c.Description,
                TrackingType = c.TrackingType,
                Unit = c.Unit,
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            var most = 0;
            foreach (var room in availableByRoom.Values)
                most = Math.Max(most, room.GetValueOrDefault(row.ComponentId));
            row.Available = most;
        }

        return rows;
    }

    private static SuggestedComponentResponse ToSuggestion(CatalogPick pick)
        => new()
        {
            ComponentId = pick.Row.ComponentId,
            ComponentCode = pick.Row.DisplayCode,
            ComponentName = pick.Row.ComponentName,
            TrackingType = pick.Row.TrackingType,
            Unit = pick.Row.Unit,
            Quantity = pick.Quantity,
            Reason = pick.Reason,
        };

    private static NotEnoughStockComponentResponse ToNotEnough(CatalogPick pick, int available)
        => new()
        {
            ComponentId = pick.Row.ComponentId,
            ComponentCode = pick.Row.DisplayCode,
            ComponentName = pick.Row.ComponentName,
            TrackingType = pick.Row.TrackingType,
            Unit = pick.Row.Unit,
            Quantity = pick.Quantity,
            Available = available,
            Reason = pick.Reason,
        };

    private static void AddSelfSupplied(
        ComponentSuggestionResponse result, HashSet<string> added, string name, int quantity, string? reason)
    {
        if (name.Length > SelfSuppliedItemRequestValidator.ItemNameMaxLength)
            name = name[..SelfSuppliedItemRequestValidator.ItemNameMaxLength];

        if (!added.Add(name.ToLower()))
            return;

        result.SelfSupplied.Add(new SelfSuppliedSuggestionResponse
        {
            ItemName = name,
            Quantity = ClampQuantity(quantity),
            Reason = CleanReason(reason),
        });
    }

    private static int ClampQuantity(int quantity)
        => Math.Clamp(quantity, 1, ProjectComponentRequestValidator.MaxQuantity);

    private static string CleanReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return string.Empty;

        var text = reason.Trim();
        if (text.Length <= ReasonMaxLength)
            return text;

        return text[..ReasonMaxLength];
    }
}

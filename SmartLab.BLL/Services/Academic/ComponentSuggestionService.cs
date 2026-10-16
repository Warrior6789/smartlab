using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.External.Ai;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Services.Inventory;
using SmartLab.BLL.Validators.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

/// <summary>
/// The AI only says what the project needs. Whether the lab has it is always read from the database:
/// unknown codes are dropped and stock never comes from the AI.
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
        var catalog = await LoadCatalogAsync(ct);

        var aiCatalog = catalog.Select(c => new AiCatalogItem
        {
            ComponentCode = c.ComponentCode,
            ComponentName = c.ComponentName,
            CategoryName = c.CategoryName,
            Description = c.Description,
            Unit = c.Unit,
            Available = c.Available,
        }).ToList();

        var advice = await _advisor.AdviseAsync(request.Description.Trim(), aiCatalog, ct);

        var byCode = catalog.ToDictionary(c => c.ComponentCode.ToUpper());
        var byName = new Dictionary<string, CatalogRow>();
        foreach (var row in catalog)
            byName.TryAdd(row.ComponentName.Trim().ToLower(), row);

        var result = new ComponentSuggestionResponse();
        var addedComponents = new HashSet<Guid>();
        var addedSelfSupplied = new HashSet<string>();

        foreach (var item in advice.InStock)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ComponentCode))
                continue;

            if (byCode.TryGetValue(item.ComponentCode.Trim().ToUpper(), out var row))
                AddCatalogSuggestion(result, addedComponents, row, item.Quantity, item.Reason);
        }

        foreach (var item in advice.SelfSupplied)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemName))
                continue;

            var name = item.ItemName.Trim();
            if (byName.TryGetValue(name.ToLower(), out var row))
            {
                AddCatalogSuggestion(result, addedComponents, row, item.Quantity, item.Reason);
                continue;
            }

            if (name.Length > SelfSuppliedItemRequestValidator.ItemNameMaxLength)
                name = name[..SelfSuppliedItemRequestValidator.ItemNameMaxLength];

            if (!addedSelfSupplied.Add(name.ToLower()))
                continue;

            result.SelfSupplied.Add(new SelfSuppliedSuggestionResponse
            {
                ItemName = name,
                Quantity = ClampQuantity(item.Quantity),
                Reason = CleanReason(item.Reason),
            });
        }

        if (result.Borrowable.Count == 0 && result.NotEnoughStock.Count == 0 && result.SelfSupplied.Count == 0)
            result.Hint = EmptyHint;

        return result;
    }

    private sealed class CatalogRow
    {
        public Guid ComponentId { get; init; }
        public string ComponentCode { get; init; } = string.Empty;
        public string ComponentName { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string TrackingType { get; init; } = string.Empty;
        public string Unit { get; init; } = string.Empty;
        public int Available { get; set; }
    }

    /// <summary>
    /// Active components with what all labs together can still lend (stock minus approved reservations, see
    /// <see cref="RoomStock"/>), the same rule as submitting and approving a project.
    /// </summary>
    private async Task<List<CatalogRow>> LoadCatalogAsync(CancellationToken ct)
    {
        var rows = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.ComponentCode)
            .Select(c => new CatalogRow
            {
                ComponentId = c.ComponentId,
                ComponentCode = c.ComponentCode,
                ComponentName = c.ComponentName,
                CategoryName = c.Category.CategoryName,
                Description = c.Description,
                TrackingType = c.TrackingType,
                Unit = c.Unit,
            })
            .ToListAsync(ct);

        var available = await RoomStock.GetTotalAvailableAsync(_uow, null, ct);
        foreach (var row in rows)
            row.Available = available.GetValueOrDefault(row.ComponentId);

        return rows;
    }

    private static void AddCatalogSuggestion(
        ComponentSuggestionResponse result, HashSet<Guid> added, CatalogRow row, int quantity, string? reason)
    {
        if (!added.Add(row.ComponentId))
            return;

        var suggestedQuantity = ClampQuantity(quantity);
        if (row.Available >= suggestedQuantity)
        {
            result.Borrowable.Add(new SuggestedComponentResponse
            {
                ComponentId = row.ComponentId,
                ComponentCode = row.ComponentCode,
                ComponentName = row.ComponentName,
                TrackingType = row.TrackingType,
                Unit = row.Unit,
                Quantity = suggestedQuantity,
                Reason = CleanReason(reason),
            });
        }
        else
        {
            result.NotEnoughStock.Add(new NotEnoughStockComponentResponse
            {
                ComponentId = row.ComponentId,
                ComponentCode = row.ComponentCode,
                ComponentName = row.ComponentName,
                TrackingType = row.TrackingType,
                Unit = row.Unit,
                Quantity = suggestedQuantity,
                Available = row.Available,
                Reason = CleanReason(reason),
            });
        }
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

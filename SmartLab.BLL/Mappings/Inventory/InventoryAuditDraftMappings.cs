using System.Text.Json;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Mappings.Inventory;

/// <summary>The draft is stored as JSON in inventory_audits.draft.</summary>
public static class InventoryAuditDraftMappings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string ToJson(SaveInventoryAuditDraftRequest request)
    {
        var draft = new AuditDraftResponse
        {
            Lines = request.Lines
                .Select(l => new AuditDraftLineResponse { ComponentId = l.ComponentId, CountedQuantity = l.CountedQuantity })
                .ToList(),
            MissingItemIds = request.MissingItemIds.ToList(),
            Extras = request.Extras
                .Select(e => new AuditDraftExtraResponse { ItemCode = e.ItemCode, ComponentId = e.ComponentId, Note = e.Note })
                .ToList(),
        };
        return JsonSerializer.Serialize(draft, JsonOptions);
    }

    public static AuditDraftResponse? FromJson(string? json, DateTime? savedAt)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var draft = JsonSerializer.Deserialize<AuditDraftResponse>(json, JsonOptions);
        if (draft == null)
            return null;

        draft.SavedAt = savedAt;
        return draft;
    }
}

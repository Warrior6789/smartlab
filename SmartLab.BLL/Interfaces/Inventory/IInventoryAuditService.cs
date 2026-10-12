using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface IInventoryAuditService
{
    /// <summary>Paged audits, newest first, filtered by cabinet, room and status.</summary>
    Task<PagedResult<InventoryAuditResponse>> GetAuditsAsync(InventoryAuditQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>Audit with the checklist to count (InProgress) or the differences it wrote (Completed).</summary>
    Task<InventoryAuditDetailResponse> GetAuditByIdAsync(Guid auditId, CancellationToken ct = default);

    /// <summary>Starts counting one cabinet. One InProgress audit per cabinet at a time.</summary>
    Task<InventoryAuditDetailResponse> StartAuditAsync(StartInventoryAuditRequest request, CancellationToken ct = default);

    /// <summary>
    /// Applies the count: consumables that differ are adjusted, missing units become Lost, each change is written
    /// to inventory_transactions with this audit id. Everything succeeds or nothing does.
    /// </summary>
    Task<InventoryAuditDetailResponse> CompleteAuditAsync(Guid auditId, CompleteInventoryAuditRequest request, CancellationToken ct = default);

    /// <summary>Discards an InProgress audit (nothing was written to stock yet).</summary>
    Task CancelAuditAsync(Guid auditId, CancellationToken ct = default);
}

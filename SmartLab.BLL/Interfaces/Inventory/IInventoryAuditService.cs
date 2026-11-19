using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface IInventoryAuditService
{
    /// <summary>
    /// Paged audits, newest first, filtered by cabinet, room and status. Lab Staff only see audits of their labs.
    /// </summary>
    Task<PagedResult<InventoryAuditResponse>> GetAuditsAsync(InventoryAuditQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>
    /// Audit with the checklist to count (InProgress), the submitted count (Submitted) or the differences it wrote (Completed).
    /// </summary>
    Task<InventoryAuditDetailResponse> GetAuditByIdAsync(Guid auditId, CancellationToken ct = default);

    /// <summary>Starts counting one cabinet. One open (InProgress or Submitted) audit per cabinet at a time.</summary>
    Task<InventoryAuditDetailResponse> StartAuditAsync(StartInventoryAuditRequest request, CancellationToken ct = default);

    /// <summary>Sends the count for approval (InProgress → Submitted). Stock is not touched yet.</summary>
    Task<InventoryAuditDetailResponse> SubmitAuditAsync(Guid auditId, SubmitInventoryAuditRequest request, CancellationToken ct = default);

    /// <summary>
    /// Accepts a submitted count (Submitted → Completed): consumables that differ are adjusted, missing units become Lost,
    /// each change is written to inventory_transactions with this audit id. The approver must not be the person who
    /// counted (Admin may). Reserved stock is never a reason to refuse; the response lists the shortages it causes.
    /// </summary>
    Task<InventoryAuditDetailResponse> ApproveAuditAsync(Guid auditId, ApproveInventoryAuditRequest request, CancellationToken ct = default);

    /// <summary>Sends a submitted count back to be counted again (Submitted → InProgress); the submitted lines are dropped.</summary>
    Task<InventoryAuditDetailResponse> RequestRecountAsync(Guid auditId, RecountInventoryAuditRequest request, CancellationToken ct = default);

    /// <summary>Discards an InProgress audit (nothing was written to stock yet).</summary>
    Task CancelAuditAsync(Guid auditId, CancellationToken ct = default);
}

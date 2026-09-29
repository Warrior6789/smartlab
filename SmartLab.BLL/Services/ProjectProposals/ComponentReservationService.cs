using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ProjectProposals;
using SmartLab.BLL.Interfaces.ProjectProposals;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.ProjectProposals;

public class ComponentReservationService : IComponentReservationService
{
    private readonly IUnitOfWork _uow;

    public ComponentReservationService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> ReserveAsync(
        IReadOnlyList<ProposalComponentRequest> components, CancellationToken ct = default)
    {
        var requestedIds = components.Select(c => c.ComponentId).Distinct().ToList();
        var activeComponents = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => requestedIds.Contains(c.ComponentId) && c.IsActive)
            .Select(c => new { c.ComponentId, c.ComponentCode })
            .ToDictionaryAsync(c => c.ComponentId, c => c.ComponentCode, ct);

        var unavailableComponentId = requestedIds.FirstOrDefault(id => !activeComponents.ContainsKey(id));
        if (unavailableComponentId != Guid.Empty)
            throw new BadRequestException($"Linh kiện '{unavailableComponentId}' không tồn tại hoặc đã ngừng hoạt động");

        var reservations = new Dictionary<Guid, IReadOnlyList<Guid>>();
        var itemRepository = _uow.Repository<ComponentItem>();

        foreach (var request in components.OrderBy(c => c.ComponentId))
        {
            var candidateIds = await itemRepository.QueryNoTracking()
                .Where(item => item.ComponentId == request.ComponentId &&
                               item.Status == ComponentItemStatuses.Available)
                .OrderBy(item => item.ImportedAt)
                .ThenBy(item => item.ItemId)
                .Select(item => item.ItemId)
                .Take(request.Quantity)
                .ToListAsync(ct);

            if (candidateIds.Count != request.Quantity)
                throw new ConflictException(
                    $"Linh kiện {activeComponents[request.ComponentId]} không đủ số lượng khả dụng");

            var now = DateTime.UtcNow;
            var updated = await itemRepository.Query()
                .Where(item => candidateIds.Contains(item.ItemId) &&
                               item.Status == ComponentItemStatuses.Available)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, ComponentItemStatuses.Reserved)
                    .SetProperty(item => item.UpdatedAt, now), ct);

            if (updated != request.Quantity)
                throw new ConflictException(
                    $"Kho linh kiện {activeComponents[request.ComponentId]} vừa thay đổi, vui lòng gửi lại yêu cầu");

            reservations[request.ComponentId] = candidateIds;
        }

        return reservations;
    }

    public async Task ReleaseAsync(IEnumerable<Guid> componentItemIds, CancellationToken ct = default)
    {
        var ids = componentItemIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        var now = DateTime.UtcNow;
        await _uow.Repository<ComponentItem>().Query()
            .Where(item => ids.Contains(item.ItemId) && item.Status == ComponentItemStatuses.Reserved)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, ComponentItemStatuses.Available)
                .SetProperty(item => item.UpdatedAt, now), ct);
    }
}

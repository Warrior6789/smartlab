using SmartLab.BLL.DTOs.ProjectProposals;

namespace SmartLab.BLL.Interfaces.ProjectProposals;

public interface IComponentReservationService
{
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> ReserveAsync(
        IReadOnlyList<ProposalComponentRequest> components, CancellationToken ct = default);

    Task ReleaseAsync(IEnumerable<Guid> componentItemIds, CancellationToken ct = default);
}

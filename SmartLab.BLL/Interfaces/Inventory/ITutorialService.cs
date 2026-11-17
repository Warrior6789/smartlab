using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface ITutorialService
{
    /// <summary>Tutorials of a component, newest first.</summary>
    Task<List<TutorialResponse>> GetByComponentAsync(Guid componentId, CancellationToken ct = default);

    /// <summary>The current user becomes the author.</summary>
    Task<TutorialResponse> CreateAsync(Guid componentId, TutorialRequest request, CancellationToken ct = default);

    /// <summary>Only the author or an Admin may update.</summary>
    Task<TutorialResponse> UpdateAsync(Guid componentId, Guid tutorialId, TutorialRequest request, CancellationToken ct = default);

    /// <summary>Hard delete; only the author or an Admin may delete.</summary>
    Task DeleteAsync(Guid componentId, Guid tutorialId, CancellationToken ct = default);
}

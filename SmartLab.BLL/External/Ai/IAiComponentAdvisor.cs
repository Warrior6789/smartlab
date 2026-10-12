namespace SmartLab.BLL.External.Ai;

/// <summary>
/// Asks an AI model which components a project needs: catalog components (by code) and items to buy outside the lab.
/// Implementations throw ServiceUnavailableException when the provider is not configured, overloaded or down,
/// and BadRequestException when the provider refuses the description.
/// </summary>
public interface IAiComponentAdvisor
{
    Task<AiComponentAdvice> AdviseAsync(string projectDescription, IReadOnlyList<AiCatalogItem> catalog, CancellationToken ct = default);
}

using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Interfaces.Academic;

public interface IComponentSuggestionService
{
    /// <summary>
    /// AI reads the project description and suggests catalog components (split by stock in the chosen lab) and
    /// items to buy outside the lab. Read-only: nothing is saved.
    /// </summary>
    Task<ComponentSuggestionResponse> SuggestAsync(SuggestComponentsRequest request, CancellationToken ct = default);
}

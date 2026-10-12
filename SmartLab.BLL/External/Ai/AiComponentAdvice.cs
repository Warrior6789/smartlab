namespace SmartLab.BLL.External.Ai;

/// <summary>Raw AI answer. Not trusted: the service checks every code against the catalog and reads stock from the DB.</summary>
public class AiComponentAdvice
{
    public List<AiStockSuggestion> InStock { get; set; } = new();

    public List<AiSelfSuppliedSuggestion> SelfSupplied { get; set; } = new();
}

using SmartLab.BLL.DTOs.Public;

namespace SmartLab.BLL.Interfaces.Public;

public interface IPublicStatsService
{
    Task<PublicStatsResponse> GetStatsAsync(CancellationToken ct = default);
}

using SmartLab.BLL.DTOs.ClassSetupDTO;
namespace SmartLab.BLL.Interfaces.Academic;

public interface ISemesterService
{
    Task<SemesterResponse> CreateAsync(CreateSemesterRequest request, CancellationToken ct = default);
    Task<List<SemesterResponse>> GetAllAsync(CancellationToken ct = default);
    Task<SemesterResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SemesterResponse> UpdateAsync(Guid id, UpdateSemesterRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
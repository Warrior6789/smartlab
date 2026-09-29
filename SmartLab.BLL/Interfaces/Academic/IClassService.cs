using SmartLab.BLL.DTOs.ClassSetupDTO;
namespace SmartLab.BLL.Interfaces.Academic;

public interface IClassService
{
    Task<ClassResponse> CreateAsync(CreateClassRequest request, CancellationToken ct = default);
    Task<List<ClassResponse>> GetAllAsync(Guid? semesterId = null, CancellationToken ct = default);
    Task<ClassResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClassResponse> UpdateAsync(Guid id, UpdateClassRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
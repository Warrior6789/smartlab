using SmartLab.BLL.DTOs.ClassSetupDTO;
namespace SmartLab.BLL.Interfaces.Academic;

public interface ICourseService
{
    Task<CourseResponse> CreateAsync(CreateCourseRequest request, CancellationToken ct = default);
    Task<List<CourseResponse>> GetAllAsync(CancellationToken ct = default);
    Task<CourseResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CourseResponse> UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
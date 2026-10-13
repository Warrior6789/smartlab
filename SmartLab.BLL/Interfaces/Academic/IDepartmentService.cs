using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Interfaces.Academic;

public interface IDepartmentService
{
    Task<PagedResult<DepartmentListItemResponse>> GetDepartmentsAsync(
        DepartmentQueryParams query, PageParams paging, CancellationToken ct = default);

    Task<DepartmentDetailResponse> GetDepartmentByIdAsync(Guid departmentId, CancellationToken ct = default);

    Task<DepartmentDetailResponse> CreateDepartmentAsync(
        CreateDepartmentRequest request, CancellationToken ct = default);

    Task<DepartmentDetailResponse> UpdateDepartmentAsync(
        Guid departmentId, UpdateDepartmentRequest request, CancellationToken ct = default);

    Task<SetDepartmentHeadResult> SetDepartmentHeadAsync(
        Guid departmentId, SetDepartmentHeadRequest request, CancellationToken ct = default);

    Task<DepartmentDetailResponse> AddLecturerAsync(
        Guid departmentId, AddDepartmentLecturerRequest request, CancellationToken ct = default);

    Task<DepartmentDetailResponse> RemoveLecturerAsync(
        Guid departmentId, Guid lecturerId, CancellationToken ct = default);
}

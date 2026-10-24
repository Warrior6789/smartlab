using Microsoft.AspNetCore.Http;

using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Interfaces.Academic;

public interface IClassRosterService
{
    /// <summary>
    /// Import danh sách sinh viên từ file Excel vào lớp.
    /// Tạo User + StudentProfile + ClassStudent + role Student nếu chưa có.
    /// </summary>
    Task<ImportRosterResult> ImportStudentsAsync(Guid classId, IFormFile file, CancellationToken ct = default);

    /// <summary>
    /// Phân công giảng viên vào lớp.
    /// Tạo User + InstructorProfile + role Lecturer nếu chưa có.
    /// </summary>
    Task<AssignInstructorResponse> AssignInstructorAsync(AssignInstructorRequest request, CancellationToken ct = default);
}
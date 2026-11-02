using SmartLab.BLL.Common;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Interfaces.Academic;

public interface IClassRosterService
{
    /// <summary>
    /// Import danh sách sinh viên từ file Excel vào lớp.
    /// Tạo User + StudentProfile + ClassStudent + role Student nếu chưa có.
    /// </summary>
    Task<ImportRosterResult> ImportStudentsAsync(Guid classId, ImportRosterRequest request, CancellationToken ct = default);

    /// <summary>
    /// Phân công giảng viên vào lớp.
    /// Tạo User + InstructorProfile + role Lecturer nếu chưa có.
    /// </summary>
    Task<AssignInstructorResponse> AssignInstructorAsync(AssignInstructorRequest request, CancellationToken ct = default);

    /// <summary>
    /// Danh sách sinh viên của lớp (phân trang, tìm theo mã/tên/email, lọc trạng thái).
    /// Admin (ACADEMIC_MANAGE) hoặc giảng viên phụ trách lớp mới xem được.
    /// </summary>
    Task<PagedResult<ClassStudentResponse>> GetStudentsAsync(Guid classId, ClassStudentQueryParams query, PageParams paging, CancellationToken ct = default);

    /// <summary>Thêm 1 sinh viên vào lớp, cùng quy tắc với một dòng của roster import.</summary>
    Task<ClassStudentResponse> AddStudentAsync(Guid classId, AddClassStudentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Đổi trạng thái Studying/Dropped. Dropped bị chặn khi SV còn trong nhóm; lời mời Pending tự chuyển Rejected.
    /// Dropped sang Studying kiểm tra lại sĩ số.
    /// </summary>
    Task<ClassStudentResponse> UpdateStudentStatusAsync(Guid classId, Guid studentProfileId, UpdateClassStudentStatusRequest request, CancellationToken ct = default);

    /// <summary>Xóa dòng class_students của SV chưa dính tới nhóm nào trong lớp (không xóa tài khoản).</summary>
    Task RemoveStudentAsync(Guid classId, Guid studentProfileId, CancellationToken ct = default);
}
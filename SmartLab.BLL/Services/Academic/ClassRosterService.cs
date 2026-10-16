using ClosedXML.Excel;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.External.Security;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class ClassRosterService : IClassRosterService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;

    public ClassRosterService(IUnitOfWork uow, IPasswordHasher passwordHasher)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
    }

    public async Task<ImportRosterResult> ImportStudentsAsync(Guid classId, IFormFile file, CancellationToken ct = default)
    {
        // 1. Validate class tồn tại
        var classEntity = await _uow.Repository<Class>().Query()
            .Include(c => c.ClassStudents)
            .FirstOrDefaultAsync(c => c.ClassId == classId, ct)
            ?? throw new NotFoundException("Lớp học", classId);

        // 2. Validate file
        if (file == null || file.Length == 0)
            throw new BadRequestException("File không được để trống.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            throw new BadRequestException("Chỉ chấp nhận file Excel (.xlsx, .xls).");

        // 3. Lấy Student role
        var studentRole = await _uow.Repository<Role>().QueryNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == RoleNames.Student, ct)
            ?? throw new InvalidOperationException("Role Student chưa được seed.");

        var result = new ImportRosterResult();

        using var transaction = await _uow.BeginTransactionAsync(ct);
        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheet(1); // Sheet đầu tiên
            var rows = sheet.RowsUsed().Skip(1).ToList(); // Bỏ header row

            result.TotalRows = rows.Count;

            foreach (var row in rows)
            {
                try
                {
                    // 4. Đọc dữ liệu từng row
                    var studentCode = row.Cell(1).GetString().Trim().ToUpperInvariant();
                    var fullName = row.Cell(2).GetString().Trim();
                    var email = row.Cell(3).GetString().Trim().ToLowerInvariant();
                    var major = row.Cell(4).GetString().Trim();
                    var cohort = row.Cell(5).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(studentCode) || string.IsNullOrWhiteSpace(email))
                    {
                        result.SkippedRows++;
                        result.Errors.Add($"Row {row.RowNumber()}: Thiếu mã SV hoặc email.");
                        continue;
                    }

                    // 5. Tìm hoặc tạo User
                    var user = await _uow.Repository<User>().Query()
                        .Include(u => u.StudentProfile)
                        .Include(u => u.UserRoles)
                        .FirstOrDefaultAsync(u => u.Email == email, ct);

                    if (user == null)
                    {
                        // Tạo mới User + StudentProfile
                        var now = DateTime.UtcNow;
                        user = new User
                        {
                            Username = email.Split('@')[0],
                            Email = email,
                            FullName = fullName,
                            PasswordHash = _passwordHasher.Hash(studentCode), // mật khẩu mặc định = mã SV
                            IsActive = true,
                            CreatedAt = now,
                            UpdatedAt = now,
                            StudentProfile = new StudentProfile
                            {
                                StudentCode = studentCode,
                                Major = string.IsNullOrWhiteSpace(major) ? null : major,
                                Cohort = string.IsNullOrWhiteSpace(cohort) ? null : cohort,
                            }
                        };
                        user.UserRoles.Add(new UserRole { RoleId = studentRole.RoleId, AssignedAt = now });

                        await _uow.Repository<User>().AddAsync(user, ct);
                        await _uow.SaveChangesAsync(ct);
                        result.CreatedUsers++;
                    }

                    // 6. Kiểm tra đã ghi danh chưa
                    var alreadyEnrolled = classEntity.ClassStudents
                        .Any(cs => cs.StudentId == user.StudentProfile!.StudentProfileId);

                    if (alreadyEnrolled)
                    {
                        result.SkippedRows++;
                        continue;
                    }

                    // 7. Ghi danh vào lớp
                    var classStudent = new ClassStudent
                    {
                        ClassId = classId,
                        StudentId = user.StudentProfile!.StudentProfileId,
                        EnrolledAt = DateTime.UtcNow,
                        Status = "Studying"
                    };
                    await _uow.Repository<ClassStudent>().AddAsync(classStudent, ct);
                    result.EnrolledStudents++;
                }
                catch (Exception ex)
                {
                    result.SkippedRows++;
                    result.Errors.Add($"Row {row.RowNumber()}: {ex.Message}");
                }
            }

            await _uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return result;
    }

    public async Task<AssignInstructorResponse> AssignInstructorAsync(AssignInstructorRequest request, CancellationToken ct = default)
    {
        // 1. Validate class tồn tại
        var classEntity = await _uow.Repository<Class>().Query()
            .FirstOrDefaultAsync(c => c.ClassId == request.ClassId, ct)
            ?? throw new NotFoundException("Lớp học", request.ClassId);

        // 2. Lấy Lecturer role
        var lecturerRole = await _uow.Repository<Role>().QueryNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == RoleNames.Lecturer, ct)
            ?? throw new InvalidOperationException("Role Lecturer chưa được seed.");

        using var transaction = await _uow.BeginTransactionAsync(ct);
        try
        {
            // 3. Tìm hoặc tạo User
            var user = await _uow.Repository<User>().Query()
                .Include(u => u.InstructorProfile)
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);

            InstructorProfile instructorProfile;

            if (user == null)
            {
                // Tạo mới User + InstructorProfile
                var now = DateTime.UtcNow;
                user = new User
                {
                    Username = request.Email.Split('@')[0],
                    Email = request.Email.Trim().ToLowerInvariant(),
                    FullName = request.FullName.Trim(),
                    PasswordHash = _passwordHasher.Hash(request.InstructorCode), // mật khẩu mặc định = mã GV
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    InstructorProfile = new InstructorProfile
                    {
                        InstructorCode = request.InstructorCode.Trim().ToUpperInvariant(),
                        Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim(),
                        AcademicTitle = string.IsNullOrWhiteSpace(request.AcademicTitle) ? null : request.AcademicTitle.Trim(),
                    }
                };
                user.UserRoles.Add(new UserRole { RoleId = lecturerRole.RoleId, AssignedAt = now });

                await _uow.Repository<User>().AddAsync(user, ct);
                await _uow.SaveChangesAsync(ct);

                instructorProfile = user.InstructorProfile!;
            }
            else
            {
                // User đã có → lấy InstructorProfile
                instructorProfile = user.InstructorProfile
                    ?? throw new BadRequestException($"User với email '{request.Email}' đã tồn tại nhưng không phải giảng viên.");

                // Gán role Lecturer nếu chưa có
                var hasRole = user.UserRoles.Any(ur => ur.RoleId == lecturerRole.RoleId);
                if (!hasRole)
                {
                    user.UserRoles.Add(new UserRole { RoleId = lecturerRole.RoleId, AssignedAt = DateTime.UtcNow });
                    await _uow.SaveChangesAsync(ct);
                }
            }

            // 4. Gán instructor vào class
            classEntity.InstructorId = instructorProfile.InstructorProfileId;
            await _uow.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            return new AssignInstructorResponse
            {
                InstructorProfileId = instructorProfile.InstructorProfileId,
                InstructorCode = instructorProfile.InstructorCode,
                FullName = user.FullName,
                Email = user.Email,
                Department = instructorProfile.Department,
                AcademicTitle = instructorProfile.AcademicTitle,
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
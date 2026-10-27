using System.Net;
using ClosedXML.Excel;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.External.Email;
using SmartLab.BLL.External.Security;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class ClassRosterService : IClassRosterService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailSender _email;

    public ClassRosterService(IUnitOfWork uow, IPasswordHasher passwordHasher, IEmailSender email)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _email = email;
    }

    public async Task<ImportRosterResult> ImportStudentsAsync(Guid classId, ImportRosterRequest request, CancellationToken ct = default)
    {
        // 1. Validate lớp tồn tại
        var classEntity = await _uow.Repository<Class>().Query()
            .Include(c => c.ClassStudents)
            .FirstOrDefaultAsync(c => c.ClassId == classId, ct)
            ?? throw new NotFoundException("Lớp học", classId);

        // 2. Lấy Student role
        var studentRole = await _uow.Repository<Role>().QueryNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == RoleNames.Student, ct)
            ?? throw new InvalidOperationException("Role Student chưa được seed.");

        var result = new ImportRosterResult();

        // Lỗi 4: đếm SV đang học trước vòng lặp
        var studying = classEntity.ClassStudents.Count(cs => cs.Status == ClassStudentStatuses.Studying);

        // Lỗi 2: dùng HashSet kiểm tra trùng trong file
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Lỗi 1: danh sách tài khoản mới để gửi mail sau SaveChanges
        var newAccounts = new List<(User User, string Password)>();

        using var stream = request.File.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var rows = sheet.RowsUsed().Skip(1).ToList(); // bỏ header

        result.TotalRows = rows.Count;

        foreach (var row in rows)
        {
            var rowNo = row.RowNumber();

            // 3. Đọc dữ liệu từng row
            var studentCode = row.Cell(1).GetString().Trim().ToUpperInvariant();
            var fullName    = row.Cell(2).GetString().Trim();
            var email       = row.Cell(3).GetString().Trim().ToLowerInvariant();
            var major       = row.Cell(4).GetString().Trim();
            var cohort      = row.Cell(5).GetString().Trim();

            if (string.IsNullOrWhiteSpace(studentCode) || string.IsNullOrWhiteSpace(email))
            {
                result.SkippedRows++;
                result.Errors.Add($"Dòng {rowNo}: Thiếu mã SV hoặc email.");
                continue;
            }

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                result.SkippedRows++;
                result.Errors.Add($"Dòng {rowNo}: Email '{email}' sai định dạng.");
                continue;
            }

            // Lỗi 2a: kiểm tra trùng trong chính file
            if (!seenCodes.Add(studentCode) || !seenEmails.Add(email))
            {
                result.SkippedRows++;
                result.Errors.Add($"Dòng {rowNo}: MSSV hoặc email bị lặp trong file.");
                continue;
            }

            // 4. Tìm hoặc tạo User
            var user = await _uow.Repository<User>().Query()
                .Include(u => u.StudentProfile)
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Email == email, ct);

            if (user == null)
            {
                var username = studentCode.ToLowerInvariant();

                // Lỗi 2b: kiểm tra MSSV/username đã thuộc tài khoản email khác
                var codeTaken = await _uow.Repository<StudentProfile>().QueryNoTracking()
                    .AnyAsync(s => s.StudentCode == studentCode, ct);
                var usernameTaken = await _uow.Repository<User>().QueryNoTracking()
                    .AnyAsync(u => u.Username == username, ct);

                if (codeTaken || usernameTaken)
                {
                    result.SkippedRows++;
                    result.Errors.Add($"Dòng {rowNo}: MSSV {studentCode} đã thuộc tài khoản có email khác.");
                    continue;
                }

                // Lỗi 4: kiểm tra sĩ số trước khi tạo mới
                if (studying >= classEntity.MaxStudents)
                {
                    result.SkippedRows++;
                    result.Errors.Add($"Dòng {rowNo}: lớp đã đủ {classEntity.MaxStudents} sinh viên.");
                    continue;
                }

                var now = DateTime.UtcNow;
                // Lỗi 1: mật khẩu ngẫu nhiên
                var password = PasswordGenerator.Generate();
                user = new User
                {
                    Username = username,
                    Email = email,
                    FullName = fullName,
                    PasswordHash = _passwordHasher.Hash(password),
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    StudentProfile = new StudentProfile
                    {
                        StudentCode = studentCode,
                        Major  = string.IsNullOrWhiteSpace(major)  ? null : major,
                        Cohort = string.IsNullOrWhiteSpace(cohort) ? null : cohort,
                    }
                };
                user.UserRoles.Add(new UserRole { RoleId = studentRole.RoleId, AssignedAt = now });
                user.StudentProfile.ClassStudents.Add(new ClassStudent
                {
                    ClassId    = classId,
                    EnrolledAt = now,
                    Status     = ClassStudentStatuses.Studying,
                });

                await _uow.Repository<User>().AddAsync(user, ct);
                newAccounts.Add((user, password));
                result.CreatedUsers++;
                result.EnrolledStudents++;
                studying++;
                continue;
            }

            // Lỗi 3: user đã tồn tại nhưng không phải SV
            if (user.StudentProfile == null)
            {
                result.SkippedRows++;
                result.Errors.Add($"Dòng {rowNo}: email {email} đã thuộc tài khoản không phải sinh viên.");
                continue;
            }

            // Kiểm tra đã ghi danh chưa
            var alreadyEnrolled = classEntity.ClassStudents
                .Any(cs => cs.StudentId == user.StudentProfile.StudentProfileId);
            if (alreadyEnrolled)
            {
                result.SkippedRows++;
                continue;
            }

            // Lỗi 4: kiểm tra sĩ số trước khi ghi danh SV đã có tài khoản
            if (studying >= classEntity.MaxStudents)
            {
                result.SkippedRows++;
                result.Errors.Add($"Dòng {rowNo}: lớp đã đủ {classEntity.MaxStudents} sinh viên.");
                continue;
            }

            // Ghi danh SV đã có tài khoản
            await _uow.Repository<ClassStudent>().AddAsync(new ClassStudent
            {
                ClassId    = classId,
                StudentId  = user.StudentProfile.StudentProfileId,
                EnrolledAt = DateTime.UtcNow,
                Status     = ClassStudentStatuses.Studying,
            }, ct);
            result.EnrolledStudents++;
            studying++;
        }

        // Lỗi 2: lưu một lần duy nhất sau vòng lặp
        await _uow.SaveChangesAsync(ct);

        // Lỗi 1: gửi mail sau khi lưu thành công
        foreach (var (account, password) in newAccounts)
            await SendAccountEmailAsync(account, password, ct);

        return result;
    }

    public async Task<AssignInstructorResponse> AssignInstructorAsync(AssignInstructorRequest request, CancellationToken ct = default)
    {
        // 1. Validate lớp tồn tại
        var classEntity = await _uow.Repository<Class>().Query()
            .FirstOrDefaultAsync(c => c.ClassId == request.ClassId, ct)
            ?? throw new NotFoundException("Lớp học", request.ClassId);

        // 2. Lấy Lecturer role
        var lecturerRole = await _uow.Repository<Role>().QueryNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == RoleNames.Lecturer, ct)
            ?? throw new InvalidOperationException("Role Lecturer chưa được seed.");

        // 3. Tìm hoặc tạo User
        var emailNorm = request.Email.Trim().ToLowerInvariant();
        var user = await _uow.Repository<User>().Query()
            .Include(u => u.InstructorProfile)
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == emailNorm, ct);

        InstructorProfile instructorProfile;

        if (user == null)
        {
            var now = DateTime.UtcNow;
            // Lỗi 1: mật khẩu ngẫu nhiên cho GV mới
            var password = PasswordGenerator.Generate();
            user = new User
            {
                Username = request.InstructorCode.Trim().ToLowerInvariant(),
                Email = emailNorm,
                FullName = request.FullName.Trim(),
                PasswordHash = _passwordHasher.Hash(password),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                InstructorProfile = new InstructorProfile
                {
                    InstructorCode = request.InstructorCode.Trim().ToUpperInvariant(),
                    AcademicTitle  = string.IsNullOrWhiteSpace(request.AcademicTitle) ? null : request.AcademicTitle.Trim(),
                }
            };
            user.UserRoles.Add(new UserRole { RoleId = lecturerRole.RoleId, AssignedAt = now });

            await _uow.Repository<User>().AddAsync(user, ct);
            await _uow.SaveChangesAsync(ct);

            instructorProfile = user.InstructorProfile!;

            // Lỗi 1: gửi mail cho GV mới sau SaveChanges
            await SendAccountEmailAsync(user, password, ct);
        }
        else
        {
            instructorProfile = user.InstructorProfile
                ?? throw new BadRequestException($"User với email '{request.Email}' đã tồn tại nhưng không phải giảng viên.");

            var hasRole = user.UserRoles.Any(ur => ur.RoleId == lecturerRole.RoleId);
            if (!hasRole)
            {
                user.UserRoles.Add(new UserRole { RoleId = lecturerRole.RoleId, AssignedAt = DateTime.UtcNow });
                await _uow.SaveChangesAsync(ct);
            }
        }

        // 4. Gán instructor vào lớp
        classEntity.InstructorId = instructorProfile.InstructorProfileId;
        await _uow.SaveChangesAsync(ct);

        return new AssignInstructorResponse
        {
            InstructorProfileId = instructorProfile.InstructorProfileId,
            InstructorCode = instructorProfile.InstructorCode,
            FullName = user.FullName,
            Email = user.Email,
            AcademicTitle = instructorProfile.AcademicTitle,
        };
    }

    private Task SendAccountEmailAsync(User user, string password, CancellationToken ct)
    {
        var body =
            $"<p>Xin chào <b>{WebUtility.HtmlEncode(user.FullName)}</b>,</p>" +
            "<p>Tài khoản SmartLab của bạn đã được tạo:</p>" +
            $"<p>Tên đăng nhập: <b>{WebUtility.HtmlEncode(user.Username)}</b><br/>" +
            $"Mật khẩu: <b>{WebUtility.HtmlEncode(password)}</b></p>" +
            "<p>Bạn có thể đổi mật khẩu trong trang cá nhân sau khi đăng nhập.</p>";

        return _email.SendAsync(user.Email, "SmartLab - Tài khoản của bạn", body, ct);
    }
}

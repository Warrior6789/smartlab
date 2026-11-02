using System.Net;
using ClosedXML.Excel;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using Npgsql;

using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.External.CurrentUser;
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
    private readonly ICurrentUserService _currentUser;

    public ClassRosterService(
        IUnitOfWork uow, IPasswordHasher passwordHasher, IEmailSender email, ICurrentUserService currentUser)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _email = email;
        _currentUser = currentUser;
    }

    private sealed record RosterRow(string StudentCode, string FullName, string Email, string? Major, string? Cohort);

    /// <summary>State shared by every row handled in one import or one add: the class, the Student role and how many seats are taken.</summary>
    private sealed class RosterBatch
    {
        public RosterBatch(Class classEntity, Role studentRole)
        {
            Class = classEntity;
            StudentRole = studentRole;
            Studying = classEntity.ClassStudents.Count(cs => cs.Status == ClassStudentStatuses.Studying);
        }

        public Class Class { get; }
        public Role StudentRole { get; }
        public int Studying { get; set; }
    }

    /// <summary>Result of one row: an error message (nothing was changed), or the student that was enrolled and the password when the account is new.</summary>
    private sealed class RosterRowOutcome
    {
        public string? Error { get; init; }
        public User? User { get; init; }
        public StudentProfile? Student { get; init; }
        public string? Password { get; init; }

        public static RosterRowOutcome Fail(string error) => new() { Error = error };
    }

    public async Task<ImportRosterResult> ImportStudentsAsync(Guid classId, ImportRosterRequest request, CancellationToken ct = default)
    {
        var classEntity = await _uow.Repository<Class>().Query()
            .Include(c => c.ClassStudents)
            .FirstOrDefaultAsync(c => c.ClassId == classId, ct)
            ?? throw new NotFoundException("Lớp học", classId);

        var studentRole = await GetStudentRoleAsync(ct);
        var batch = new RosterBatch(classEntity, studentRole);

        var result = new ImportRosterResult();

        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var newAccounts = new List<(User User, string Password)>();

        using var stream = request.File.OpenReadStream();
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception)
        {
            throw new BadRequestException("File Excel không hợp lệ hoặc bị hỏng.");
        }

        using (workbook)
        {
            IXLWorksheet sheet;
            try
            {
                sheet = workbook.Worksheet(1);
            }
            catch (Exception)
            {
                throw new BadRequestException("File Excel không có sheet dữ liệu hợp lệ.");
            }

            var rows = sheet.RowsUsed().Skip(1).ToList();

            result.TotalRows = rows.Count;

            foreach (var row in rows)
            {
                var rowNo = row.RowNumber();

                var studentCode = row.Cell(1).GetString().Trim().ToUpperInvariant();
                var fullName = row.Cell(2).GetString().Trim();
                var email = row.Cell(3).GetString().Trim().ToLowerInvariant();
                var major = row.Cell(4).GetString().Trim();
                var cohort = row.Cell(5).GetString().Trim();

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

                if (!seenCodes.Add(studentCode) || !seenEmails.Add(email))
                {
                    result.SkippedRows++;
                    result.Errors.Add($"Dòng {rowNo}: MSSV hoặc email bị lặp trong file.");
                    continue;
                }

                var outcome = await EnrollRowAsync(batch, new RosterRow(
                    studentCode,
                    fullName,
                    email,
                    string.IsNullOrWhiteSpace(major) ? null : major,
                    string.IsNullOrWhiteSpace(cohort) ? null : cohort), ct);

                if (outcome.Error != null)
                {
                    result.SkippedRows++;
                    result.Errors.Add($"Dòng {rowNo}: {outcome.Error}");
                    continue;
                }

                if (outcome.Password != null)
                {
                    newAccounts.Add((outcome.User!, outcome.Password));
                    result.CreatedUsers++;
                }

                result.EnrolledStudents++;
            }
        }

        await _uow.SaveChangesAsync(ct);

        foreach (var (account, password) in newAccounts)
            await SendAccountEmailAsync(account, password, ct);

        return result;
    }

    public async Task<PagedResult<ClassStudentResponse>> GetStudentsAsync(
        Guid classId, ClassStudentQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        await EnsureCanViewRosterAsync(classId, ct);

        var enrollments = _uow.Repository<ClassStudent>().QueryNoTracking()
            .Where(cs => cs.ClassId == classId);

        var status = ClassStudentStatuses.Normalize(query.Status);
        if (status != null)
            enrollments = enrollments.Where(cs => cs.Status == status);

        var search = query.Search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(search))
            enrollments = enrollments.Where(cs => cs.Student.StudentCode.ToLower().Contains(search)
                                               || cs.Student.User.FullName.ToLower().Contains(search)
                                               || cs.Student.User.Email.ToLower().Contains(search));

        return await PagedResult<ClassStudentResponse>.CreateAsync(
            ToResponses(enrollments.OrderBy(cs => cs.Student.StudentCode)), paging, ct);
    }

    public async Task<ClassStudentResponse> AddStudentAsync(
        Guid classId, AddClassStudentRequest request, CancellationToken ct = default)
    {
        var studentRole = await GetStudentRoleAsync(ct);

        RosterRowOutcome outcome;
        await using (var tx = await _uow.BeginTransactionAsync(ct))
        {
            await LockClassAsync(classId, ct);

            var classEntity = await _uow.Repository<Class>().Query()
                .Include(c => c.Semester)
                .Include(c => c.ClassStudents)
                .FirstOrDefaultAsync(c => c.ClassId == classId, ct)
                ?? throw new NotFoundException("Lớp học", classId);

            if (!classEntity.Semester.IsActive)
                throw new ConflictException("Học kỳ của lớp không còn hoạt động");

            var row = new RosterRow(
                request.StudentCode.Trim().ToUpperInvariant(),
                request.FullName.Trim(),
                request.Email.Trim().ToLowerInvariant(),
                null,
                null);

            outcome = await EnrollRowAsync(new RosterBatch(classEntity, studentRole), row, ct);
            if (outcome.Error != null)
                throw new ConflictException(outcome.Error);

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                throw new ConflictException("Sinh viên, mã sinh viên hoặc email đã tồn tại");
            }

            await tx.CommitAsync(ct);
        }

        if (outcome.Password != null)
            await SendAccountEmailAsync(outcome.User!, outcome.Password, ct);

        return await GetClassStudentAsync(classId, outcome.Student!.StudentProfileId, ct);
    }

    public async Task<ClassStudentResponse> UpdateStudentStatusAsync(
        Guid classId, Guid studentProfileId, UpdateClassStudentStatusRequest request, CancellationToken ct = default)
    {
        var target = ClassStudentStatuses.Normalize(request.Status)
            ?? throw new BadRequestException($"Trạng thái phải là một trong: {string.Join(", ", ClassStudentStatuses.All)}");

        await using (var tx = await _uow.BeginTransactionAsync(ct))
        {
            await LockClassAsync(classId, ct);

            var enrollment = await _uow.Repository<ClassStudent>().Query()
                .Include(cs => cs.Class)
                .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentProfileId, ct)
                ?? throw new NotFoundException("Sinh viên không có trong lớp");

            if (enrollment.Status == target)
                throw new BadRequestException($"Sinh viên đã ở trạng thái {target}");

            if (target == ClassStudentStatuses.Dropped)
            {
                var teamName = await _uow.Repository<Team>().QueryNoTracking()
                    .Where(t => t.ClassId == classId
                             && (t.LeaderId == studentProfileId
                                 || t.TeamMembers.Any(tm => tm.StudentId == studentProfileId
                                                         && tm.Status == TeamMemberStatuses.Accepted)))
                    .Select(t => t.TeamName)
                    .FirstOrDefaultAsync(ct);
                if (teamName != null)
                    throw new ConflictException($"Sinh viên đang ở nhóm {teamName}, cần rời nhóm trước");

                var pendingInvitations = await _uow.Repository<TeamMember>().Query()
                    .Include(tm => tm.Team)
                    .Where(tm => tm.StudentId == studentProfileId
                              && tm.Status == TeamMemberStatuses.Pending
                              && tm.Team.ClassId == classId)
                    .ToListAsync(ct);

                var now = DateTime.UtcNow;
                foreach (var invitation in pendingInvitations)
                {
                    invitation.Status = TeamMemberStatuses.Rejected;
                    invitation.RespondedAt = now;
                    invitation.Team.UpdatedAt = now;
                }
            }
            else
            {
                var studying = await _uow.Repository<ClassStudent>().QueryNoTracking()
                    .CountAsync(cs => cs.ClassId == classId && cs.Status == ClassStudentStatuses.Studying, ct);
                if (studying >= enrollment.Class.MaxStudents)
                    throw new ConflictException($"Lớp đã đủ {enrollment.Class.MaxStudents} sinh viên");
            }

            enrollment.Status = target;
            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return await GetClassStudentAsync(classId, studentProfileId, ct);
    }

    public async Task RemoveStudentAsync(Guid classId, Guid studentProfileId, CancellationToken ct = default)
    {
        var enrollment = await _uow.Repository<ClassStudent>().Query()
            .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentProfileId, ct)
            ?? throw new NotFoundException("Sinh viên không có trong lớp");

        var hasTeamData = await _uow.Repository<Team>().QueryNoTracking()
                              .AnyAsync(t => t.ClassId == classId && t.LeaderId == studentProfileId, ct)
                          || await _uow.Repository<TeamMember>().QueryNoTracking()
                              .AnyAsync(tm => tm.StudentId == studentProfileId && tm.Team.ClassId == classId, ct);
        if (hasTeamData)
            throw new ConflictException("Sinh viên đã có dữ liệu nhóm, hãy chuyển sang Dropped");

        _uow.Repository<ClassStudent>().Remove(enrollment);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Handles one student of a roster: finds or creates the account, then enrolls it as Studying.
    /// Shared by the Excel import and the single add so both apply the same rules.
    /// </summary>
    private async Task<RosterRowOutcome> EnrollRowAsync(RosterBatch batch, RosterRow row, CancellationToken ct)
    {
        var classEntity = batch.Class;

        var user = await _uow.Repository<User>().Query()
            .Include(u => u.StudentProfile)
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == row.Email, ct);

        if (user == null)
        {
            var username = row.StudentCode.ToLowerInvariant();

            var codeTaken = await _uow.Repository<StudentProfile>().QueryNoTracking()
                .AnyAsync(s => s.StudentCode == row.StudentCode, ct);
            var usernameTaken = await _uow.Repository<User>().QueryNoTracking()
                .AnyAsync(u => u.Username == username, ct);

            if (codeTaken || usernameTaken)
                return RosterRowOutcome.Fail($"MSSV {row.StudentCode} đã thuộc tài khoản có email khác.");

            if (batch.Studying >= classEntity.MaxStudents)
                return RosterRowOutcome.Fail($"Lớp đã đủ {classEntity.MaxStudents} sinh viên.");

            var now = DateTime.UtcNow;
            var password = PasswordGenerator.Generate();
            user = new User
            {
                Username = username,
                Email = row.Email,
                FullName = row.FullName,
                PasswordHash = _passwordHasher.Hash(password),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                StudentProfile = new StudentProfile
                {
                    StudentCode = row.StudentCode,
                    Major = row.Major,
                    Cohort = row.Cohort,
                }
            };
            user.UserRoles.Add(new UserRole { RoleId = batch.StudentRole.RoleId, AssignedAt = now });
            user.StudentProfile.ClassStudents.Add(new ClassStudent
            {
                ClassId = classEntity.ClassId,
                EnrolledAt = now,
                Status = ClassStudentStatuses.Studying,
            });

            await _uow.Repository<User>().AddAsync(user, ct);
            batch.Studying++;
            return new RosterRowOutcome { User = user, Student = user.StudentProfile, Password = password };
        }

        if (user.StudentProfile == null)
            return RosterRowOutcome.Fail($"Email {row.Email} đã thuộc tài khoản không phải sinh viên.");

        if (!string.Equals(user.StudentProfile.StudentCode, row.StudentCode, StringComparison.OrdinalIgnoreCase))
            return RosterRowOutcome.Fail(
                $"Email {row.Email} đã thuộc sinh viên có MSSV khác ({user.StudentProfile.StudentCode}).");

        var existing = classEntity.ClassStudents
            .FirstOrDefault(cs => cs.StudentId == user.StudentProfile.StudentProfileId);
        if (existing != null)
            return RosterRowOutcome.Fail(existing.Status == ClassStudentStatuses.Dropped
                ? "Sinh viên đang ở trạng thái Dropped, hãy đổi trạng thái để cho học lại."
                : "Sinh viên đã có trong lớp.");

        if (batch.Studying >= classEntity.MaxStudents)
            return RosterRowOutcome.Fail($"Lớp đã đủ {classEntity.MaxStudents} sinh viên.");

        await _uow.Repository<ClassStudent>().AddAsync(new ClassStudent
        {
            ClassId = classEntity.ClassId,
            StudentId = user.StudentProfile.StudentProfileId,
            EnrolledAt = DateTime.UtcNow,
            Status = ClassStudentStatuses.Studying,
        }, ct);
        batch.Studying++;
        return new RosterRowOutcome { User = user, Student = user.StudentProfile };
    }

    private async Task<Role> GetStudentRoleAsync(CancellationToken ct)
        => await _uow.Repository<Role>().QueryNoTracking()
               .FirstOrDefaultAsync(r => r.RoleName == RoleNames.Student, ct)
           ?? throw new InvalidOperationException("Role Student chưa được seed.");

    /// <summary>
    /// Locks the class row (no-op UPDATE) until the transaction ends, so two admins adding students
    /// at the same time are counted one after another and cannot go over MaxStudents.
    /// </summary>
    private Task LockClassAsync(Guid classId, CancellationToken ct)
        => _uow.Repository<Class>().Query()
            .Where(c => c.ClassId == classId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.MaxStudents, c => c.MaxStudents), ct);

    /// <summary>Admins (ACADEMIC_MANAGE) see every class; a lecturer only the class they are in charge of.</summary>
    private async Task EnsureCanViewRosterAsync(Guid classId, CancellationToken ct)
    {
        var instructorUserId = await _uow.Repository<Class>().QueryNoTracking()
            .Where(c => c.ClassId == classId)
            .Select(c => new { UserId = c.Instructor != null ? (Guid?)c.Instructor.UserId : null })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Lớp học", classId);

        if (_currentUser.HasPermission(PermissionCodes.AcademicManage))
            return;

        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        if (instructorUserId.UserId != userId)
            throw new ForbiddenException("Chỉ quản trị viên hoặc giảng viên phụ trách lớp mới xem được danh sách sinh viên");
    }

    private static IQueryable<ClassStudentResponse> ToResponses(IQueryable<ClassStudent> enrollments)
        => enrollments
            .Select(cs => new ClassStudentResponse
            {
                StudentProfileId = cs.StudentId,
                UserId = cs.Student.UserId,
                StudentCode = cs.Student.StudentCode,
                FullName = cs.Student.User.FullName,
                Email = cs.Student.User.Email,
                Status = cs.Status,
                EnrolledAt = cs.EnrolledAt,
                TeamId = cs.Student.TeamMembers
                    .Where(tm => tm.Status == TeamMemberStatuses.Accepted && tm.Team.ClassId == cs.ClassId)
                    .Select(tm => (Guid?)tm.TeamId)
                    .FirstOrDefault(),
                TeamName = cs.Student.TeamMembers
                    .Where(tm => tm.Status == TeamMemberStatuses.Accepted && tm.Team.ClassId == cs.ClassId)
                    .Select(tm => tm.Team.TeamName)
                    .FirstOrDefault(),
                IsLeader = cs.Student.Teams.Any(t => t.ClassId == cs.ClassId),
            });

    private async Task<ClassStudentResponse> GetClassStudentAsync(Guid classId, Guid studentProfileId, CancellationToken ct)
        => await ToResponses(_uow.Repository<ClassStudent>().QueryNoTracking()
                .Where(cs => cs.ClassId == classId && cs.StudentId == studentProfileId))
            .FirstAsync(ct);

    // ↓↓↓ từ đây trở xuống là code cũ, GIỮ NGUYÊN: AssignInstructorAsync, SendAccountEmailAsync ↓↓↓
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

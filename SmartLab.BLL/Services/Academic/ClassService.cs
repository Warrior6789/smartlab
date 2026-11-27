using SmartLab.BLL.Interfaces.Notifications;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.BLL.Mappings.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class ClassService : IClassService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IBorrowNotifier _borrowNotifier;
    private readonly INotificationSender _notifications;

    public ClassService(
        IUnitOfWork uow, ICurrentUserService currentUser, IBorrowNotifier borrowNotifier, INotificationSender notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _borrowNotifier = borrowNotifier;
        _notifications = notifications;
    }

    // Query helper — luôn include đủ để mapping
    private IQueryable<Class> QueryWithIncludes(bool tracking = false)
    {
        var repo = _uow.Repository<Class>();
        var q = tracking ? repo.Query() : repo.QueryNoTracking();
        return q
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .Include(c => c.Instructor).ThenInclude(i => i!.User)
            .Include(c => c.ClassStudents)
            .AsSplitQuery();
    }

    public async Task<ClassResponse> CreateAsync(CreateClassRequest request, CancellationToken ct = default)
    {
        // Validate foreign keys tồn tại
        var courseCode = await _uow.Repository<Course>().QueryNoTracking()
            .Where(c => c.CourseId == request.CourseId)
            .Select(c => c.CourseCode)
            .FirstOrDefaultAsync(ct);
        if (courseCode == null)
            throw new NotFoundException("Môn học", request.CourseId);

        var semesterCode = await _uow.Repository<Semester>().QueryNoTracking()
            .Where(s => s.SemesterId == request.SemesterId)
            .Select(s => s.SemesterCode)
            .FirstOrDefaultAsync(ct);
        if (semesterCode == null)
            throw new NotFoundException("Kỳ học", request.SemesterId);

        var classCode = request.ClassCode?.Trim() ?? string.Empty;
        if (classCode.Length == 0)
            classCode = await NextClassCodeAsync(request.SemesterId, courseCode, semesterCode, ct);

        // Lỗi 7: kiểm tra InstructorId tồn tại (giống UpdateAsync)
        if (request.InstructorId.HasValue)
        {
            var instructorExists = await _uow.Repository<InstructorProfile>().QueryNoTracking()
                .AnyAsync(i => i.InstructorProfileId == request.InstructorId.Value, ct);
            if (!instructorExists)
                throw new NotFoundException("Giảng viên", request.InstructorId.Value);
        }

        // Check trùng ClassCode trong cùng semester
        var duplicate = await _uow.Repository<Class>().QueryNoTracking()
            .AnyAsync(c => c.SemesterId == request.SemesterId
                        && c.ClassCode == classCode, ct);
        if (duplicate)
            throw new ConflictException($"Mã lớp '{classCode}' đã tồn tại trong kỳ học này.");

        int maxStudents;
        if (request.MaxStudents.HasValue)
        {
            maxStudents = request.MaxStudents.Value;
            await ClassSizeRules.EnsureWithinLimitAsync(_uow, maxStudents, ct);
        }
        else
        {
            maxStudents = await ClassSizeRules.GetDefaultMaxStudentsAsync(_uow, ct);
        }

        var entity = new Class
        {
            ClassCode = classCode,
            CourseId = request.CourseId,
            SemesterId = request.SemesterId,
            InstructorId = request.InstructorId,
            MaxStudents = maxStudents,
            CreatedAt = DateTime.UtcNow,
        };

        await _uow.Repository<Class>().AddAsync(entity, ct);
        // Lỗi 8: bắt race condition unique violation
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException($"Mã lớp '{classCode}' đã tồn tại trong kỳ học này.");
        }

        await ClassLecturerNotice.SendAsync(_uow, _notifications, entity.ClassId, null, entity.InstructorId, ct);

        // Reload với include để mapping đầy đủ
        var created = await QueryWithIncludes()
            .FirstAsync(c => c.ClassId == entity.ClassId, ct);
        return ClassSetupMappings.ToResponse(created);
    }

    public async Task<List<ClassResponse>> GetAllAsync(Guid? semesterId = null, CancellationToken ct = default)
    {
        var query = QueryWithIncludes();
        if (semesterId.HasValue)
            query = query.Where(c => c.SemesterId == semesterId.Value);

        var list = await query.OrderBy(c => c.ClassCode).ToListAsync(ct);
        return list.Select(ClassSetupMappings.ToResponse).ToList();
    }

    public async Task<List<ClassResponse>> GetMyClassesAsync(Guid? semesterId = null, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var query = QueryWithIncludes()
            .Where(c => (c.Instructor != null && c.Instructor.UserId == userId)
                        || c.ClassStudents.Any(cs => cs.Student.UserId == userId
                                                     && cs.Status == ClassStudentStatuses.Studying));
        if (semesterId.HasValue)
            query = query.Where(c => c.SemesterId == semesterId.Value);

        var list = await query
            .OrderByDescending(c => c.Semester.StartDate)
            .ThenBy(c => c.ClassCode)
            .ToListAsync(ct);
        return list.Select(ClassSetupMappings.ToResponse).ToList();
    }

    public async Task<ClassResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await QueryWithIncludes()
            .FirstOrDefaultAsync(c => c.ClassId == id, ct)
            ?? throw new NotFoundException("Lớp học", id);
        return ClassSetupMappings.ToResponse(entity);
    }

    public async Task<List<ClassStudentResponse>> GetStudentsAsync(Guid classId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var cls = await _uow.Repository<Class>().QueryNoTracking()
            .Where(c => c.ClassId == classId)
            .Select(c => new { InstructorUserId = c.Instructor != null ? (Guid?)c.Instructor.UserId : null })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Lớp học", classId);

        // The lecturer of this class, or an academic manager (Admin); other lecturers may not see it.
        if (cls.InstructorUserId != userId && !_currentUser.HasPermission(PermissionCodes.AcademicManage))
            throw new ForbiddenException("Bạn chỉ xem được danh sách sinh viên của lớp mình phụ trách");

        var students = await _uow.Repository<ClassStudent>().QueryNoTracking()
            .Where(cs => cs.ClassId == classId)
            .Select(cs => new
            {
                cs.StudentId,
                cs.Student.UserId,
                cs.Student.StudentCode,
                cs.Student.User.FullName,
                cs.Student.User.Email,
                cs.Student.User.AvatarUrl,
                cs.Student.User.IsActive,
                ActivationPending = !cs.Student.User.IsActive
                                    && cs.Student.User.VerificationCodes.Any(c => c.Purpose == VerificationPurposes.EmailVerification && c.UsedAt == null),
                cs.Student.Major,
                cs.Student.Cohort,
                cs.Status,
                cs.EnrolledAt,
            })
            .ToListAsync(ct);

        var memberships = await _uow.Repository<TeamMember>().QueryNoTracking()
            .Where(m => m.Team.ClassId == classId && m.Status == TeamMemberStatuses.Accepted)
            .Select(m => new { m.StudentId, m.TeamId, m.Team.TeamName, m.Team.LeaderId })
            .ToListAsync(ct);
        var teamOf = memberships
            .GroupBy(m => m.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        return students
            .Select(s =>
            {
                teamOf.TryGetValue(s.StudentId, out var team);
                return new ClassStudentResponse
                {
                    StudentProfileId = s.StudentId,
                    UserId = s.UserId,
                    StudentCode = s.StudentCode,
                    FullName = s.FullName,
                    Email = s.Email,
                    AvatarUrl = s.AvatarUrl,
                    IsActive = s.IsActive,
                    ActivationPending = s.ActivationPending,
                    Major = s.Major,
                    Cohort = s.Cohort,
                    Status = s.Status,
                    EnrolledAt = s.EnrolledAt,
                    TeamId = team?.TeamId,
                    TeamName = team?.TeamName,
                    IsTeamLeader = team != null && team.LeaderId == s.StudentId,
                };
            })
            .OrderBy(s => s.Status == ClassStudentStatuses.Studying ? 0 : 1)
            .ThenBy(s => s.StudentCode)
            .ToList();
    }

    public async Task<ClassResponse> UpdateAsync(Guid id, UpdateClassRequest request, CancellationToken ct = default)
    {
        var entity = await QueryWithIncludes(tracking: true)
            .FirstOrDefaultAsync(c => c.ClassId == id, ct)
            ?? throw new NotFoundException("Lớp học", id);

        // Lỗi 9a: kiểm tra CourseId mới tồn tại
        var courseExists = await _uow.Repository<Course>().QueryNoTracking()
            .AnyAsync(c => c.CourseId == request.CourseId, ct);
        if (!courseExists)
            throw new NotFoundException("Môn học", request.CourseId);

        Guid? lecturerUserId = null;
        if (request.InstructorId.HasValue)
        {
            lecturerUserId = await _uow.Repository<InstructorProfile>().QueryNoTracking()
                                 .Where(i => i.InstructorProfileId == request.InstructorId.Value)
                                 .Select(i => (Guid?)i.UserId)
                                 .FirstOrDefaultAsync(ct)
                             ?? throw new NotFoundException("Giảng viên", request.InstructorId.Value);
        }

        // Lỗi 9b: không được hạ MaxStudents xuống dưới số SV đang học
        var studying = entity.ClassStudents.Count(cs => cs.Status == ClassStudentStatuses.Studying);
        if (request.MaxStudents < studying)
            throw new BadRequestException($"Lớp đang có {studying} sinh viên đang học, sĩ số tối đa không được nhỏ hơn.");

        if (request.MaxStudents != entity.MaxStudents)
            await ClassSizeRules.EnsureWithinLimitAsync(_uow, request.MaxStudents, ct);

        entity.ClassCode = request.ClassCode.Trim();
        entity.CourseId = request.CourseId;          // Lỗi 9a: bổ sung dòng bị thiếu
        entity.MaxStudents = request.MaxStudents;
        var previousInstructorId = entity.InstructorId;
        if (request.InstructorId.HasValue)
            entity.InstructorId = request.InstructorId;

        var movedRequestIds = new List<Guid>();
        await using (var tx = await _uow.BeginTransactionAsync(ct))
        {
            await _uow.SaveChangesAsync(ct);
            if (lecturerUserId.HasValue)
                movedRequestIds = await PendingApprovals.MoveToLecturerAsync(_uow, id, lecturerUserId.Value, ct);
            await tx.CommitAsync(ct);
        }

        foreach (var requestId in movedRequestIds)
            await _borrowNotifier.SubmittedAsync(requestId, ct);
        await ClassLecturerNotice.SendAsync(_uow, _notifications, id, previousInstructorId, entity.InstructorId, ct);

        return ClassSetupMappings.ToResponse(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<Class>().Query()
            .Include(c => c.ClassStudents)
            .FirstOrDefaultAsync(c => c.ClassId == id, ct)
            ?? throw new NotFoundException("Lớp học", id);

        if (entity.ClassStudents.Any())
            throw new ConflictException("Không thể xóa lớp đang có sinh viên đã ghi danh.");

        _uow.Repository<Class>().Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>Next free code COURSE_SEMESTER_NN in the semester, e.g. IOT201_FA26_01, IOT201_FA26_02.</summary>
    private async Task<string> NextClassCodeAsync(Guid semesterId, string courseCode, string semesterCode, CancellationToken ct)
    {
        var prefix = $"{courseCode}_{semesterCode}_".ToUpper();
        var used = await _uow.Repository<Class>().QueryNoTracking()
            .Where(c => c.SemesterId == semesterId && c.ClassCode.ToUpper().StartsWith(prefix))
            .Select(c => c.ClassCode)
            .ToListAsync(ct);

        var taken = new HashSet<string>(used.Select(c => c.ToUpper()));
        var number = 1;
        while (taken.Contains($"{prefix}{number:D2}"))
            number++;
        return $"{prefix}{number:D2}";
    }
}

using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Mappings.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class ClassService : IClassService
{
    private readonly IUnitOfWork _uow;

    public ClassService(IUnitOfWork uow) => _uow = uow;

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
        var courseExists = await _uow.Repository<Course>().QueryNoTracking()
            .AnyAsync(c => c.CourseId == request.CourseId, ct);
        if (!courseExists)
            throw new NotFoundException("Môn học", request.CourseId);

        var semesterExists = await _uow.Repository<Semester>().QueryNoTracking()
            .AnyAsync(s => s.SemesterId == request.SemesterId, ct);
        if (!semesterExists)
            throw new NotFoundException("Kỳ học", request.SemesterId);

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
                        && c.ClassCode == request.ClassCode.Trim(), ct);
        if (duplicate)
            throw new ConflictException($"Mã lớp '{request.ClassCode}' đã tồn tại trong kỳ học này.");

        var entity = new Class
        {
            ClassCode = request.ClassCode.Trim(),
            CourseId = request.CourseId,
            SemesterId = request.SemesterId,
            InstructorId = request.InstructorId,
            MaxStudents = request.MaxStudents ?? 30,
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
            throw new ConflictException($"Mã lớp '{request.ClassCode}' đã tồn tại trong kỳ học này.");
        }

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

    public async Task<ClassResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await QueryWithIncludes()
            .FirstOrDefaultAsync(c => c.ClassId == id, ct)
            ?? throw new NotFoundException("Lớp học", id);
        return ClassSetupMappings.ToResponse(entity);
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

        if (request.InstructorId.HasValue)
        {
            var instructorExists = await _uow.Repository<InstructorProfile>().QueryNoTracking()
                .AnyAsync(i => i.InstructorProfileId == request.InstructorId.Value, ct);
            if (!instructorExists)
                throw new NotFoundException("Giảng viên", request.InstructorId.Value);
        }

        // Lỗi 9b: không được hạ MaxStudents xuống dưới số SV đang học
        var studying = entity.ClassStudents.Count(cs => cs.Status == ClassStudentStatuses.Studying);
        if (request.MaxStudents < studying)
            throw new BadRequestException($"Lớp đang có {studying} sinh viên đang học, sĩ số tối đa không được nhỏ hơn.");

        entity.ClassCode = request.ClassCode.Trim();
        entity.CourseId = request.CourseId;          // Lỗi 9a: bổ sung dòng bị thiếu
        entity.MaxStudents = request.MaxStudents;
        entity.InstructorId = request.InstructorId;

        await _uow.SaveChangesAsync(ct);
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
}
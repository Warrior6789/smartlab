using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Mappings.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class CourseService : ICourseService
{
    private readonly IUnitOfWork _uow;

    public CourseService(IUnitOfWork uow) => _uow = uow;

    public async Task<CourseResponse> CreateAsync(CreateCourseRequest request, CancellationToken ct = default)
    {
        var exists = await _uow.Repository<Course>().QueryNoTracking()
            .AnyAsync(c => c.CourseCode == request.CourseCode.Trim(), ct);
        if (exists)
            throw new ConflictException($"Mã môn học '{request.CourseCode}' đã tồn tại.");

        var course = new Course
        {
            CourseCode = request.CourseCode.Trim().ToUpperInvariant(),
            CourseName = request.CourseName.Trim(),
            Credits = request.Credits,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive,
        };

        await _uow.Repository<Course>().AddAsync(course, ct);
        await _uow.SaveChangesAsync(ct);
        return ClassSetupMappings.ToResponse(course);
    }

    public async Task<List<CourseResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _uow.Repository<Course>().QueryNoTracking()
            .OrderBy(c => c.CourseCode)
            .ToListAsync(ct);
        return list.Select(ClassSetupMappings.ToResponse).ToList();
    }

    public async Task<CourseResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var course = await _uow.Repository<Course>().GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Môn học", id);
        return ClassSetupMappings.ToResponse(course);
    }

    public async Task<CourseResponse> UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken ct = default)
    {
        var course = await _uow.Repository<Course>().Query()
            .FirstOrDefaultAsync(c => c.CourseId == id, ct)
            ?? throw new NotFoundException("Môn học", id);

        course.CourseName = request.CourseName.Trim();
        course.Credits = request.Credits;
        course.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        course.IsActive = request.IsActive;

        await _uow.SaveChangesAsync(ct);
        return ClassSetupMappings.ToResponse(course);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var course = await _uow.Repository<Course>().Query()
            .Include(c => c.Classes)
            .FirstOrDefaultAsync(c => c.CourseId == id, ct)
            ?? throw new NotFoundException("Môn học", id);

        if (course.Classes.Any())
            throw new ConflictException("Không thể xóa môn học đang có lớp đang sử dụng.");

        _uow.Repository<Course>().Remove(course);
        await _uow.SaveChangesAsync(ct);
    }
}
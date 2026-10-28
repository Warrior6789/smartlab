using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Mappings.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class SemesterService : ISemesterService
{
    private readonly IUnitOfWork _uow;

    public SemesterService(IUnitOfWork uow) => _uow = uow;

    public async Task<SemesterResponse> CreateAsync(CreateSemesterRequest request, CancellationToken ct = default)
    {
        // Lỗi 6: chuẩn hóa một lần, dùng cho cả check và lưu
        var code = request.SemesterCode.Trim();

        var exists = await _uow.Repository<Semester>().QueryNoTracking()
            .AnyAsync(s => s.SemesterCode == code, ct);
        if (exists)
            throw new ConflictException($"Mã kỳ học '{code}' đã tồn tại.");

        var semester = new Semester
        {
            SemesterCode = code,
            Term = request.Term.Trim(),
            Year = request.Year,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
        };

        await _uow.Repository<Semester>().AddAsync(semester, ct);
        // Lỗi 8: bắt race condition unique violation
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException($"Mã kỳ học '{code}' đã tồn tại.");
        }
        return ClassSetupMappings.ToResponse(semester);
    }

    public async Task<List<SemesterResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _uow.Repository<Semester>().QueryNoTracking()
            .OrderByDescending(s => s.Year).ThenBy(s => s.Term)
            .ToListAsync(ct);
        return list.Select(ClassSetupMappings.ToResponse).ToList();
    }

    public async Task<SemesterResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var semester = await _uow.Repository<Semester>().GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Kỳ học", id);
        return ClassSetupMappings.ToResponse(semester);
    }

    public async Task<SemesterResponse> UpdateAsync(Guid id, UpdateSemesterRequest request, CancellationToken ct = default)
    {
        var semester = await _uow.Repository<Semester>().Query()
            .FirstOrDefaultAsync(s => s.SemesterId == id, ct)
            ?? throw new NotFoundException("Kỳ học", id);

        semester.Term = request.Term.Trim();
        semester.Year = request.Year;
        semester.StartDate = request.StartDate;
        semester.EndDate = request.EndDate;
        semester.IsActive = request.IsActive;

        await _uow.SaveChangesAsync(ct);
        return ClassSetupMappings.ToResponse(semester);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var semester = await _uow.Repository<Semester>().Query()
            .Include(s => s.Classes)
            .FirstOrDefaultAsync(s => s.SemesterId == id, ct)
            ?? throw new NotFoundException("Kỳ học", id);

        if (semester.Classes.Any())
            throw new ConflictException("Không thể xóa kỳ học đang có lớp học.");

        _uow.Repository<Semester>().Remove(semester);
        await _uow.SaveChangesAsync(ct);
    }
}
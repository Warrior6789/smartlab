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

    private static DateOnly TodayInVietnam() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    /// <summary>Spring = starts January to April, Summer = May to August, Fall = September to December.</summary>
    private static string TermFor(DateOnly startDate)
    {
        if (startDate.Month <= 4)
            return "Spring";
        if (startDate.Month <= 8)
            return "Summer";
        return "Fall";
    }

    private static string CodePrefixFor(string term)
    {
        if (term == "Spring")
            return "SP";
        if (term == "Summer")
            return "SU";
        return "FA";
    }

    /// <summary>FA26, or FA26_A, FA26_B... when that code is taken (semesters running side by side).</summary>
    private async Task<string> NextFreeCodeAsync(string term, int year, CancellationToken ct)
    {
        var baseCode = $"{CodePrefixFor(term)}{year % 100:00}";
        var taken = await _uow.Repository<Semester>().QueryNoTracking()
            .Where(s => s.SemesterCode.ToUpper().StartsWith(baseCode))
            .Select(s => s.SemesterCode.ToUpper())
            .ToListAsync(ct);

        if (!taken.Contains(baseCode))
            return baseCode;

        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            var candidate = $"{baseCode}_{letter}";
            if (!taken.Contains(candidate))
                return candidate;
        }

        throw new ConflictException($"Đã hết mã tự sinh cho {baseCode}, hãy nhập mã kỳ học.");
    }

    public async Task<SemesterResponse> CreateAsync(CreateSemesterRequest request, CancellationToken ct = default)
    {
        if (request.StartDate < TodayInVietnam())
            throw new BadRequestException("Không thể tạo kỳ học trong quá khứ: ngày bắt đầu phải từ hôm nay trở đi.");

        var term = TermFor(request.StartDate);
        var code = request.SemesterCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            code = await NextFreeCodeAsync(term, request.StartDate.Year, ct);
        }
        else
        {
            var exists = await _uow.Repository<Semester>().QueryNoTracking()
                .AnyAsync(s => s.SemesterCode.ToUpper() == code, ct);
            if (exists)
                throw new ConflictException($"Mã kỳ học '{code}' đã tồn tại.");
        }

        var semester = new Semester
        {
            SemesterCode = code,
            Term = term,
            Year = request.StartDate.Year,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
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

        var today = TodayInVietnam();
        if (request.StartDate != semester.StartDate && request.StartDate < today)
            throw new BadRequestException("Ngày bắt đầu mới không được ở quá khứ.");
        if (request.EndDate != semester.EndDate && request.EndDate < today)
            throw new BadRequestException("Ngày kết thúc mới không được ở quá khứ.");

        semester.Term = TermFor(request.StartDate);
        semester.Year = request.StartDate.Year;
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
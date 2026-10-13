using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Mappings.Academic;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class DepartmentService : IDepartmentService
{
    private const string VietnameseCollation = "vi-VN-x-icu";
    private const string DepartmentNameUniqueConstraint = "departments_name_key";

    private readonly IUnitOfWork _uow;

    public DepartmentService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public Task<PagedResult<DepartmentListItemResponse>> GetDepartmentsAsync(
        DepartmentQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var departments = _uow.Repository<Department>().QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = EscapeLikePattern(query.Search.Trim());
            departments = departments.Where(department =>
                EF.Functions.ILike(department.DepartmentName, $"%{keyword}%", "\\"));
        }

        var result = departments
            .OrderBy(department => EF.Functions.Collate(department.DepartmentName, VietnameseCollation))
            .ThenBy(department => department.DepartmentId)
            .Select(DepartmentMappings.ListItemProjection);

        return PagedResult<DepartmentListItemResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<DepartmentDetailResponse> GetDepartmentByIdAsync(
        Guid departmentId, CancellationToken ct = default)
    {
        var department = await _uow.Repository<Department>().QueryNoTracking()
            .Include(item => item.Head)
            .Include(item => item.InstructorProfiles)
                .ThenInclude(profile => profile.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.DepartmentId == departmentId, ct)
            ?? throw new NotFoundException("Không tìm thấy bộ môn");

        return DepartmentMappings.ToDetailResponse(department);
    }

    public async Task<DepartmentDetailResponse> CreateDepartmentAsync(
        CreateDepartmentRequest request, CancellationToken ct = default)
    {
        var departmentName = request.DepartmentName.Trim();
        await EnsureDepartmentNameAvailableAsync(departmentName, excludedDepartmentId: null, ct);

        var now = DateTime.UtcNow;
        var department = new Department
        {
            DepartmentName = departmentName,
            HeadId = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await _uow.Repository<Department>().AddAsync(department, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDepartmentNameUniqueViolation(ex))
        {
            throw new ConflictException("Tên bộ môn đã tồn tại");
        }

        return await GetDepartmentByIdAsync(department.DepartmentId, ct);
    }

    public async Task<DepartmentDetailResponse> UpdateDepartmentAsync(
        Guid departmentId, UpdateDepartmentRequest request, CancellationToken ct = default)
    {
        var department = await _uow.Repository<Department>().Query()
            .FirstOrDefaultAsync(item => item.DepartmentId == departmentId, ct)
            ?? throw new NotFoundException("Không tìm thấy bộ môn");

        var departmentName = request.DepartmentName.Trim();
        if (string.Equals(department.DepartmentName, departmentName, StringComparison.Ordinal))
            return await GetDepartmentByIdAsync(departmentId, ct);

        await EnsureDepartmentNameAvailableAsync(departmentName, departmentId, ct);
        department.DepartmentName = departmentName;
        department.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDepartmentNameUniqueViolation(ex))
        {
            throw new ConflictException("Tên bộ môn đã tồn tại");
        }

        return await GetDepartmentByIdAsync(departmentId, ct);
    }

    public async Task<SetDepartmentHeadResult> SetDepartmentHeadAsync(
        Guid departmentId, SetDepartmentHeadRequest request, CancellationToken ct = default)
    {
        var department = await _uow.Repository<Department>().Query()
            .Include(item => item.Head)
            .FirstOrDefaultAsync(item => item.DepartmentId == departmentId, ct)
            ?? throw new NotFoundException("Không tìm thấy bộ môn");

        if (!request.HeadId.HasValue)
        {
            if (!department.HeadId.HasValue)
            {
                return new SetDepartmentHeadResult
                {
                    Department = await GetDepartmentByIdAsync(departmentId, ct),
                    Message = $"Bộ môn {department.DepartmentName} hiện chưa có Trưởng bộ môn.",
                };
            }

            department.HeadId = null;
            department.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(ct);

            return new SetDepartmentHeadResult
            {
                Department = await GetDepartmentByIdAsync(departmentId, ct),
                Message = "Gỡ Trưởng bộ môn thành công",
            };
        }

        var headId = request.HeadId.Value;
        if (headId == Guid.Empty)
            throw new BadRequestException("headId không được là Guid rỗng");

        if (department.HeadId == headId)
        {
            var headName = department.Head?.FullName ?? "Giảng viên này";
            return new SetDepartmentHeadResult
            {
                Department = await GetDepartmentByIdAsync(departmentId, ct),
                Message = $"Giảng viên {headName} hiện đã là Trưởng bộ môn {department.DepartmentName}.",
            };
        }

        var profile = await _uow.Repository<InstructorProfile>().QueryNoTracking()
            .Include(item => item.User)
                .ThenInclude(user => user.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(item => item.UserId == headId, ct)
            ?? throw new NotFoundException("Tài khoản không có hồ sơ giảng viên");

        if (!profile.User.IsActive)
            throw new BadRequestException("Tài khoản giảng viên đang bị khóa");

        if (!HasLecturerRole(profile.User))
            throw new BadRequestException("Tài khoản không có role Lecturer");

        if (profile.DepartmentId != departmentId)
            throw new BadRequestException("Giảng viên chưa thuộc bộ môn này; hãy thêm giảng viên vào bộ môn trước");

        department.HeadId = headId;
        department.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return new SetDepartmentHeadResult
        {
            Department = await GetDepartmentByIdAsync(departmentId, ct),
            Message = "Cập nhật Trưởng bộ môn thành công",
        };
    }

    public async Task<DepartmentDetailResponse> AddLecturerAsync(
        Guid departmentId, AddDepartmentLecturerRequest request, CancellationToken ct = default)
    {
        await using var transaction = await _uow.BeginTransactionAsync(ct);

        try
        {
            var departments = _uow.Repository<Department>();
            var targetDepartment = await departments.Query()
                .FirstOrDefaultAsync(item => item.DepartmentId == departmentId, ct)
                ?? throw new NotFoundException("Không tìm thấy bộ môn");

            var user = await _uow.Repository<User>().Query()
                .Include(item => item.InstructorProfile)
                .Include(item => item.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
                .FirstOrDefaultAsync(item => item.UserId == request.LecturerId, ct)
                ?? throw new NotFoundException("Không tìm thấy người dùng");

            var profile = user.InstructorProfile
                ?? throw new NotFoundException("Tài khoản không có hồ sơ giảng viên");

            if (!user.IsActive)
                throw new BadRequestException("Tài khoản giảng viên đang bị khóa");

            if (!HasLecturerRole(user))
                throw new BadRequestException("Tài khoản không có role Lecturer");

            if (profile.DepartmentId == departmentId)
                throw new ConflictException("Giảng viên đã thuộc bộ môn này");

            Department? sourceDepartment = null;
            if (profile.DepartmentId.HasValue)
            {
                sourceDepartment = await departments.Query()
                    .FirstOrDefaultAsync(item => item.DepartmentId == profile.DepartmentId.Value, ct)
                    ?? throw new InvalidOperationException("Dữ liệu bộ môn hiện tại của giảng viên không nhất quán");

                if (sourceDepartment.HeadId == request.LecturerId)
                    throw new BadRequestException("Giảng viên đang là Trưởng bộ môn khác; hãy gỡ hoặc đổi Trưởng bộ môn trước");
            }

            var now = DateTime.UtcNow;
            profile.DepartmentId = departmentId;
            targetDepartment.UpdatedAt = now;
            if (sourceDepartment is not null)
                sourceDepartment.UpdatedAt = now;

            await _uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetDepartmentByIdAsync(departmentId, ct);
    }

    public async Task<DepartmentDetailResponse> RemoveLecturerAsync(
        Guid departmentId, Guid lecturerId, CancellationToken ct = default)
    {
        await using var transaction = await _uow.BeginTransactionAsync(ct);

        try
        {
            var department = await _uow.Repository<Department>().Query()
                .FirstOrDefaultAsync(item => item.DepartmentId == departmentId, ct)
                ?? throw new NotFoundException("Không tìm thấy bộ môn");

            var profile = await _uow.Repository<InstructorProfile>().Query()
                .FirstOrDefaultAsync(item => item.UserId == lecturerId && item.DepartmentId == departmentId, ct)
                ?? throw new NotFoundException("Giảng viên không thuộc bộ môn này");

            if (department.HeadId == lecturerId)
                throw new BadRequestException("Không thể gỡ giảng viên đang là Trưởng bộ môn; hãy gỡ hoặc đổi Trưởng bộ môn trước");

            profile.DepartmentId = null;
            department.UpdatedAt = DateTime.UtcNow;

            await _uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetDepartmentByIdAsync(departmentId, ct);
    }

    private async Task EnsureDepartmentNameAvailableAsync(
        string departmentName, Guid? excludedDepartmentId, CancellationToken ct)
    {
        var normalizedName = departmentName.ToLower();
        var duplicateExists = await _uow.Repository<Department>().QueryNoTracking()
            .AnyAsync(item => item.DepartmentName.ToLower() == normalizedName
                              && (!excludedDepartmentId.HasValue || item.DepartmentId != excludedDepartmentId.Value), ct);

        if (duplicateExists)
            throw new ConflictException("Tên bộ môn đã tồn tại");
    }

    private static bool HasLecturerRole(User user)
        => user.UserRoles.Any(userRole => userRole.Role.RoleName == RoleNames.Lecturer);

    private static bool IsDepartmentNameUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: DepartmentNameUniqueConstraint,
        };

    private static string EscapeLikePattern(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}

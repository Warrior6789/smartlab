using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.BLL.Mappings.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;

    public CategoryService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await ToResponses(_uow.Repository<ComponentCategory>().QueryNoTracking()
                .OrderBy(c => EF.Functions.Collate(c.CategoryName, DbCollations.Vietnamese)))
            .ToListAsync(ct);
    }

    public async Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var name = request.CategoryName.Trim();
        var categories = _uow.Repository<ComponentCategory>();

        if (await categories.QueryNoTracking().AnyAsync(c => c.CategoryName.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Danh mục đã tồn tại");

        var category = new ComponentCategory
        {
            CategoryName = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        };

        try
        {
            await categories.AddAsync(category, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Danh mục đã tồn tại");
        }

        return CategoryMappings.ToCategoryResponse(category);
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var categories = _uow.Repository<ComponentCategory>();
        var category = await categories.Query().FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct)
                       ?? throw new NotFoundException("Không tìm thấy danh mục");

        var name = request.CategoryName.Trim();
        if (await categories.QueryNoTracking().AnyAsync(c => c.CategoryId != categoryId && c.CategoryName.ToLower() == name.ToLower(), ct))
            throw new ConflictException("Danh mục đã tồn tại");

        category.CategoryName = name;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Danh mục đã tồn tại");
        }

        return await ToResponses(categories.QueryNoTracking().Where(c => c.CategoryId == categoryId)).FirstAsync(ct);
    }

    public async Task DeleteCategoryAsync(Guid categoryId, CancellationToken ct = default)
    {
        var categories = _uow.Repository<ComponentCategory>();
        var category = await categories.Query().FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct)
                       ?? throw new NotFoundException("Không tìm thấy danh mục");

        if (await _uow.Repository<Component>().QueryNoTracking().AnyAsync(c => c.CategoryId == categoryId, ct))
            throw new ConflictException("Danh mục còn linh kiện, hãy chuyển linh kiện sang danh mục khác trước khi xóa");

        categories.Remove(category);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            throw new ConflictException("Danh mục còn linh kiện, hãy chuyển linh kiện sang danh mục khác trước khi xóa");
        }
    }

    private static IQueryable<CategoryResponse> ToResponses(IQueryable<ComponentCategory> categories) =>
        categories.Select(c => new CategoryResponse
        {
            CategoryId = c.CategoryId,
            CategoryName = c.CategoryName,
            Description = c.Description,
            ComponentCount = c.Components.Count,
        });
}

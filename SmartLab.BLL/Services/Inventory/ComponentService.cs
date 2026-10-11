using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.External.Storage;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class ComponentService : IComponentService
{
    private const string DatasheetStorageFolder = "datasheets";
    private const string ComponentCodePrefix = "LK-";
    private const string DefaultUnit = "cái";
    private const string ComponentCodeUniqueConstraint = "components_component_code_key";
    private const string ComponentNameUniqueConstraint = "components_name_key";
    private const string DuplicateNameMessage = "Tên linh kiện đã tồn tại";

    /// <summary>
    /// Concurrent creates can compute the same next code; the losers regenerate it and save again.
    /// Each round at least one request wins, so this covers up to this many simultaneous creates.
    /// </summary>
    private const int MaxCodeAttempts = 10;

    private readonly IUnitOfWork _uow;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<ComponentService> _logger;

    public ComponentService(IUnitOfWork uow, IFileStorage fileStorage, ILogger<ComponentService> logger)
    {
        _uow = uow;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public Task<PagedResult<ComponentResponse>> GetComponentsAsync(
        ComponentQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var components = _uow.Repository<Component>().QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            components = components.Where(c => c.ComponentCode.ToLower().Contains(keyword)
                                            || c.ComponentName.ToLower().Contains(keyword)
                                            || (c.Manufacturer != null && c.Manufacturer.ToLower().Contains(keyword)));
        }

        if (query.CategoryId.HasValue)
            components = components.Where(c => c.CategoryId == query.CategoryId.Value);

        if (query.IsActive.HasValue)
            components = components.Where(c => c.IsActive == query.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(query.TrackingType))
        {
            var trackingType = query.TrackingType.Trim().ToUpper();
            components = components.Where(c => c.TrackingType == trackingType);
        }

        if (query.LowStock == true)
        {
            components = components.Where(c => c.MinStock != null
                                            && (c.TrackingType == ComponentTrackingTypes.Consumable
                                                ? c.ConsumableStocks.Sum(st => st.Quantity)
                                                : c.ComponentItems.Count(i => i.Status != ComponentItemStatuses.Lost && i.Status != ComponentItemStatuses.Retired))
                                            < c.MinStock);
        }

        var result = components
            .OrderBy(c => EF.Functions.Collate(c.ComponentName, DbCollations.Vietnamese)).ThenBy(c => c.ComponentId)
            .Select(c => new ComponentResponse
            {
                ComponentId = c.ComponentId,
                ComponentCode = c.ComponentCode,
                ComponentName = c.ComponentName,
                CategoryId = c.CategoryId,
                CategoryName = c.Category.CategoryName,
                Manufacturer = c.Manufacturer,
                PrimaryImageUrl = c.ComponentImages.Where(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault(),
                IsActive = c.IsActive,
                TrackingType = c.TrackingType,
                Unit = c.Unit,
                TotalQuantity = c.TrackingType == ComponentTrackingTypes.Consumable
                    ? c.ConsumableStocks.Sum(st => st.Quantity)
                    : c.ComponentItems.Count(i => i.Status != ComponentItemStatuses.Lost && i.Status != ComponentItemStatuses.Retired),
                AvailableQuantity = c.TrackingType == ComponentTrackingTypes.Consumable
                    ? c.ConsumableStocks.Sum(st => st.Quantity)
                    : c.ComponentItems.Count(i => i.Status == ComponentItemStatuses.Available),
                MinStock = c.MinStock,
                LatestUnitPrice = c.StockReceiptItems
                    .Where(i => i.Receipt.Status == StockReceiptStatuses.Completed)
                    .OrderByDescending(i => i.Receipt.ReceivedAt)
                    .Select(i => (decimal?)i.UnitPrice)
                    .FirstOrDefault(),
            });

        return PagedResult<ComponentResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<ComponentDetailResponse> GetComponentByIdAsync(Guid componentId, CancellationToken ct = default)
    {
        var component = await _uow.Repository<Component>().QueryNoTracking()
                            .Where(c => c.ComponentId == componentId)
                            .Select(c => new ComponentDetailResponse
                            {
                                ComponentId = c.ComponentId,
                                ComponentCode = c.ComponentCode,
                                ComponentName = c.ComponentName,
                                CategoryId = c.CategoryId,
                                CategoryName = c.Category.CategoryName,
                                Manufacturer = c.Manufacturer,
                                PrimaryImageUrl = c.ComponentImages.Where(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault(),
                                IsActive = c.IsActive,
                                TrackingType = c.TrackingType,
                                Unit = c.Unit,
                                TotalQuantity = c.TrackingType == ComponentTrackingTypes.Consumable
                                    ? c.ConsumableStocks.Sum(st => st.Quantity)
                                    : c.ComponentItems.Count(i => i.Status != ComponentItemStatuses.Lost && i.Status != ComponentItemStatuses.Retired),
                                AvailableQuantity = c.TrackingType == ComponentTrackingTypes.Consumable
                                    ? c.ConsumableStocks.Sum(st => st.Quantity)
                                    : c.ComponentItems.Count(i => i.Status == ComponentItemStatuses.Available),
                                MinStock = c.MinStock,
                                LatestUnitPrice = c.StockReceiptItems
                                    .Where(i => i.Receipt.Status == StockReceiptStatuses.Completed)
                                    .OrderByDescending(i => i.Receipt.ReceivedAt)
                                    .Select(i => (decimal?)i.UnitPrice)
                                    .FirstOrDefault(),
                                Description = c.Description,
                                DatasheetUrl = c.DatasheetUrl,
                                CreatedAt = c.CreatedAt,
                                UpdatedAt = c.UpdatedAt,
                                Specs = c.ComponentSpecs
                                    .OrderBy(s => s.SpecKey)
                                    .Select(s => new ComponentSpecResponse
                                    {
                                        SpecId = s.SpecId,
                                        SpecKey = s.SpecKey,
                                        SpecValue = s.SpecValue,
                                        Unit = s.Unit,
                                    })
                                    .ToList(),
                                Images = c.ComponentImages
                                    .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.CreatedAt).ThenBy(i => i.ImageId)
                                    .Select(i => new ComponentImageResponse
                                    {
                                        ImageId = i.ImageId,
                                        ImageUrl = i.ImageUrl,
                                        ImageType = i.ImageType,
                                        IsPrimary = i.IsPrimary,
                                        CreatedAt = i.CreatedAt,
                                    })
                                    .ToList(),
                            })
                            .FirstOrDefaultAsync(ct)
                        ?? throw new NotFoundException("Không tìm thấy linh kiện");

        if (component.TrackingType == ComponentTrackingTypes.Consumable)
        {
            component.Stocks = await _uow.Repository<ConsumableStock>().QueryNoTracking()
                .Where(st => st.ComponentId == componentId && st.Quantity > 0)
                .Select(st => new CabinetStockResponse
                {
                    CabinetId = st.CabinetId,
                    CabinetCode = st.Cabinet.CabinetCode,
                    CabinetName = st.Cabinet.CabinetName,
                    RoomId = st.Cabinet.RoomId,
                    RoomCode = st.Cabinet.Room.RoomCode,
                    RoomName = st.Cabinet.Room.RoomName,
                    TotalQuantity = st.Quantity,
                    AvailableQuantity = st.Quantity,
                })
                .OrderBy(st => st.CabinetCode)
                .ToListAsync(ct);
        }
        else
        {
            component.Stocks = await _uow.Repository<ComponentItem>().QueryNoTracking()
                .Where(i => i.ComponentId == componentId
                            && i.Status != ComponentItemStatuses.Lost
                            && i.Status != ComponentItemStatuses.Retired)
                .GroupBy(i => new { i.CabinetId, i.Cabinet.CabinetCode, i.Cabinet.CabinetName, i.Cabinet.RoomId, i.Cabinet.Room.RoomCode, i.Cabinet.Room.RoomName })
                .Select(g => new CabinetStockResponse
                {
                    CabinetId = g.Key.CabinetId,
                    CabinetCode = g.Key.CabinetCode,
                    CabinetName = g.Key.CabinetName,
                    RoomId = g.Key.RoomId,
                    RoomCode = g.Key.RoomCode,
                    RoomName = g.Key.RoomName,
                    TotalQuantity = g.Count(),
                    AvailableQuantity = g.Count(i => i.Status == ComponentItemStatuses.Available),
                })
                .OrderBy(s => s.CabinetCode)
                .ToListAsync(ct);
        }

        var datasheetKey = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => c.ComponentId == componentId)
            .Select(c => c.DatasheetStorageKey)
            .FirstOrDefaultAsync(ct);
        if (datasheetKey != null)
            component.DatasheetPreviewUrl = _fileStorage.GetPdfPreviewUrl(datasheetKey);

        return component;
    }

    public async Task<ComponentDetailResponse> CreateComponentAsync(CreateComponentRequest request, CancellationToken ct = default)
    {
        var components = _uow.Repository<Component>();

        await EnsureCategoryExistsAsync(request.CategoryId, ct);

        var name = request.ComponentName.Trim();
        await EnsureNameNotTakenAsync(name, null, ct);

        var now = DateTime.UtcNow;
        var component = new Component
        {
            CategoryId = request.CategoryId,
            ComponentCode = await GenerateComponentCodeAsync(ct),
            ComponentName = name,
            Manufacturer = string.IsNullOrWhiteSpace(request.Manufacturer) ? null : request.Manufacturer.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DatasheetUrl = string.IsNullOrWhiteSpace(request.DatasheetUrl) ? null : request.DatasheetUrl.Trim(),
            TrackingType = request.TrackingType,
            Unit = CleanUnit(request.Unit),
            MinStock = request.MinStock,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        foreach (var spec in request.Specs)
            component.ComponentSpecs.Add(NewSpec(spec));

        await components.AddAsync(component, ct);

        var attempt = 1;
        while (true)
        {
            try
            {
                await _uow.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                               {
                                                   SqlState: PostgresErrorCodes.UniqueViolation,
                                                   ConstraintName: ComponentCodeUniqueConstraint,
                                               })
            {
                if (attempt == MaxCodeAttempts)
                    throw new ConflictException("Có nhiều linh kiện đang được tạo cùng lúc, vui lòng thử lại");

                attempt++;
                component.ComponentCode = await GenerateComponentCodeAsync(ct);
            }
            catch (DbUpdateException ex) when (IsNameConflict(ex))
            {
                throw new ConflictException(DuplicateNameMessage);
            }
        }

        return await GetComponentByIdAsync(component.ComponentId, ct);
    }

    public async Task<ComponentDetailResponse> UpdateComponentAsync(
        Guid componentId, UpdateComponentRequest request, CancellationToken ct = default)
    {
        var component = await _uow.Repository<Component>().Query()
                            .Include(c => c.ComponentSpecs)
                            .FirstOrDefaultAsync(c => c.ComponentId == componentId, ct)
                        ?? throw new NotFoundException("Không tìm thấy linh kiện");

        if (component.CategoryId != request.CategoryId)
            await EnsureCategoryExistsAsync(request.CategoryId, ct);

        var name = request.ComponentName.Trim();
        await EnsureNameNotTakenAsync(name, componentId, ct);

        component.CategoryId = request.CategoryId;
        component.ComponentName = name;
        component.Manufacturer = string.IsNullOrWhiteSpace(request.Manufacturer) ? null : request.Manufacturer.Trim();
        component.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var datasheetUrl = string.IsNullOrWhiteSpace(request.DatasheetUrl) ? null : request.DatasheetUrl.Trim();
        string? replacedDatasheetKey = null;
        if (component.DatasheetStorageKey != null && datasheetUrl != component.DatasheetUrl)
        {
            replacedDatasheetKey = component.DatasheetStorageKey;
            component.DatasheetStorageKey = null;
        }
        component.DatasheetUrl = datasheetUrl;
        component.Unit = CleanUnit(request.Unit);
        component.MinStock = request.MinStock;
        component.IsActive = request.IsActive!.Value;
        component.UpdatedAt = DateTime.UtcNow;

        var specs = _uow.Repository<ComponentSpec>();
        foreach (var old in component.ComponentSpecs.ToList())
            specs.Remove(old);
        foreach (var spec in request.Specs!)
            component.ComponentSpecs.Add(NewSpec(spec));

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsNameConflict(ex))
        {
            throw new ConflictException(DuplicateNameMessage);
        }

        if (replacedDatasheetKey != null)
            await TryDeleteStoredFileAsync(replacedDatasheetKey);

        return await GetComponentByIdAsync(component.ComponentId, ct);
    }

    public async Task<ComponentDetailResponse> UploadDatasheetAsync(
        Guid componentId, UploadDatasheetRequest request, CancellationToken ct = default)
    {
        var component = await _uow.Repository<Component>().Query()
                            .FirstOrDefaultAsync(c => c.ComponentId == componentId, ct)
                        ?? throw new NotFoundException("Không tìm thấy linh kiện");

        StoredFile stored;
        await using (var content = request.File.OpenReadStream())
        {
            stored = await _fileStorage.UploadPdfAsync(content, request.File.FileName, DatasheetStorageFolder, ct);
        }

        var oldKey = component.DatasheetStorageKey;
        component.DatasheetUrl = stored.Url;
        component.DatasheetStorageKey = stored.Key;
        component.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            await TryDeleteStoredFileAsync(stored.Key);
            throw;
        }

        if (oldKey != null)
            await TryDeleteStoredFileAsync(oldKey);

        return await GetComponentByIdAsync(componentId, ct);
    }

    public async Task DeleteDatasheetAsync(Guid componentId, CancellationToken ct = default)
    {
        var component = await _uow.Repository<Component>().Query()
                            .FirstOrDefaultAsync(c => c.ComponentId == componentId, ct)
                        ?? throw new NotFoundException("Không tìm thấy linh kiện");
        if (component.DatasheetUrl == null)
            throw new NotFoundException("Linh kiện chưa có datasheet");

        var oldKey = component.DatasheetStorageKey;
        component.DatasheetUrl = null;
        component.DatasheetStorageKey = null;
        component.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        if (oldKey != null)
            await TryDeleteStoredFileAsync(oldKey);
    }

    /// <summary>Best-effort delete of a stored file; a failure is only logged so the request still succeeds.</summary>
    private async Task TryDeleteStoredFileAsync(string key)
    {
        try
        {
            await _fileStorage.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete stored file {StorageKey}", key);
        }
    }

    private static string CleanUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            return DefaultUnit;
        return unit.Trim();
    }

    /// <summary>Next code: LK-0001, LK-0002, ... (highest existing number + 1).</summary>
    private async Task<string> GenerateComponentCodeAsync(CancellationToken ct)
    {
        var existingCodes = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => c.ComponentCode.StartsWith(ComponentCodePrefix))
            .Select(c => c.ComponentCode)
            .ToListAsync(ct);

        var maxNumber = 0;
        foreach (var code in existingCodes)
        {
            if (int.TryParse(code.Substring(ComponentCodePrefix.Length), out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"{ComponentCodePrefix}{maxNumber + 1:D4}";
    }

    /// <summary>Component names are unique, case-insensitive; excludeId skips the component being updated.</summary>
    private async Task EnsureNameNotTakenAsync(string name, Guid? excludeId, CancellationToken ct)
    {
        var lowerName = name.ToLower();
        var taken = await _uow.Repository<Component>().QueryNoTracking()
            .AnyAsync(c => c.ComponentId != excludeId && c.ComponentName.ToLower() == lowerName, ct);
        if (taken)
            throw new ConflictException(DuplicateNameMessage);
    }

    /// <summary>Unique index components_name_key on lower(component_name) catches two requests racing past the check above.</summary>
    private static bool IsNameConflict(DbUpdateException ex)
        => ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ComponentNameUniqueConstraint,
        };

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken ct)
    {
        if (!await _uow.Repository<ComponentCategory>().QueryNoTracking().AnyAsync(c => c.CategoryId == categoryId, ct))
            throw new NotFoundException("Không tìm thấy danh mục");
    }

    private static ComponentSpec NewSpec(ComponentSpecRequest spec) => new()
    {
        SpecKey = spec.SpecKey.Trim(),
        SpecValue = spec.SpecValue.Trim(),
        Unit = string.IsNullOrWhiteSpace(spec.Unit) ? null : spec.Unit.Trim(),
        IsAiExtracted = false,
    };
}

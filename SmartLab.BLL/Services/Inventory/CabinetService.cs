using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class CabinetService : ICabinetService
{
    private readonly IUnitOfWork _uow;

    public CabinetService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public Task<PagedResult<CabinetResponse>> GetCabinetsAsync(
        CabinetQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var cabinets = _uow.Repository<StorageCabinet>().QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            cabinets = cabinets.Where(c => c.CabinetCode.ToLower().Contains(keyword)
                                        || c.CabinetName.ToLower().Contains(keyword));
        }

        if (query.RoomId.HasValue)
            cabinets = cabinets.Where(c => c.RoomId == query.RoomId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            cabinets = cabinets.Where(c => c.Status == status);
        }

        var result = ToResponses(cabinets
            .OrderBy(c => c.Room.RoomCode).ThenBy(c => c.CabinetCode).ThenBy(c => c.CabinetId));

        return PagedResult<CabinetResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<CabinetDetailResponse> GetCabinetByIdAsync(Guid cabinetId, CancellationToken ct = default)
    {
        var cabinet = await _uow.Repository<StorageCabinet>().QueryNoTracking()
                          .Where(c => c.CabinetId == cabinetId)
                          .Select(c => new CabinetDetailResponse
                          {
                              CabinetId = c.CabinetId,
                              CabinetCode = c.CabinetCode,
                              CabinetName = c.CabinetName,
                              RoomId = c.RoomId,
                              RoomCode = c.Room.RoomCode,
                              RoomName = c.Room.RoomName,
                              Location = c.Location,
                              Status = c.Status,
                              ItemCount = c.ComponentItems.Count(i => i.Status != ComponentItemStatuses.Lost
                                                                      && i.Status != ComponentItemStatuses.Retired)
                                          + c.ConsumableStocks.Sum(st => st.Quantity),
                          })
                          .FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Không tìm thấy tủ");

        var itemContents = await _uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.CabinetId == cabinetId
                        && i.Status != ComponentItemStatuses.Lost
                        && i.Status != ComponentItemStatuses.Retired)
            .GroupBy(i => new { i.ComponentId, i.Component.ComponentCode, i.Component.ComponentName, i.Component.TrackingType })
            .Select(g => new CabinetContentResponse
            {
                ComponentId = g.Key.ComponentId,
                ComponentCode = g.Key.ComponentCode,
                ComponentName = g.Key.ComponentName,
                TrackingType = g.Key.TrackingType,
                TotalQuantity = g.Count(),
                AvailableQuantity = g.Count(i => i.Status == ComponentItemStatuses.Available),
            })
            .ToListAsync(ct);

        var consumableContents = await _uow.Repository<ConsumableStock>().QueryNoTracking()
            .Where(st => st.CabinetId == cabinetId && st.Quantity > 0)
            .Select(st => new CabinetContentResponse
            {
                ComponentId = st.ComponentId,
                ComponentCode = st.Component.ComponentCode,
                ComponentName = st.Component.ComponentName,
                TrackingType = st.Component.TrackingType,
                TotalQuantity = st.Quantity,
                AvailableQuantity = st.Quantity,
            })
            .ToListAsync(ct);

        cabinet.Contents = itemContents.Concat(consumableContents)
            .OrderBy(x => x.ComponentCode)
            .ToList();

        return cabinet;
    }

    public async Task<CabinetResponse> CreateCabinetAsync(CreateCabinetRequest request, CancellationToken ct = default)
    {
        var roomCode = await _uow.Repository<LabRoom>().QueryNoTracking()
                           .Where(r => r.RoomId == request.RoomId && r.Status == LabRoomStatuses.Active)
                           .Select(r => r.RoomCode)
                           .FirstOrDefaultAsync(ct)
                       ?? throw new BadRequestException("Phòng lab không tồn tại hoặc đã ngừng sử dụng");

        var cabinets = _uow.Repository<StorageCabinet>();
        var cabinet = new StorageCabinet
        {
            CabinetCode = await GenerateCabinetCodeAsync(roomCode, ct),
            CabinetName = request.CabinetName.Trim(),
            RoomId = request.RoomId,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Status = CabinetStatuses.Active,
        };

        try
        {
            await cabinets.AddAsync(cabinet, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Có tủ khác vừa được tạo trong phòng này, vui lòng thử lại");
        }

        return await ToResponses(cabinets.QueryNoTracking().Where(c => c.CabinetId == cabinet.CabinetId)).FirstAsync(ct);
    }

    public async Task<CabinetResponse> UpdateCabinetAsync(Guid cabinetId, UpdateCabinetRequest request, CancellationToken ct = default)
    {
        var cabinets = _uow.Repository<StorageCabinet>();
        var cabinet = await cabinets.Query().FirstOrDefaultAsync(c => c.CabinetId == cabinetId, ct)
                      ?? throw new NotFoundException("Không tìm thấy tủ");

        if (request.RoomId != cabinet.RoomId)
            await EnsureActiveRoomAsync(request.RoomId, ct);

        if (request.Status == CabinetStatuses.Inactive && cabinet.Status != CabinetStatuses.Inactive
            && await _uow.Repository<ComponentItem>().QueryNoTracking().AnyAsync(i => i.CabinetId == cabinetId
                   && i.Status != ComponentItemStatuses.Lost && i.Status != ComponentItemStatuses.Retired, ct))
            throw new ConflictException("Tủ còn linh kiện, hãy chuyển linh kiện sang tủ khác trước khi ngừng sử dụng");

        if (request.Status == CabinetStatuses.Inactive && cabinet.Status != CabinetStatuses.Inactive
            && await _uow.Repository<ConsumableStock>().QueryNoTracking().AnyAsync(st => st.CabinetId == cabinetId && st.Quantity > 0, ct))
            throw new ConflictException("Tủ còn linh kiện tiêu hao, hãy chuyển sang tủ khác trước khi ngừng sử dụng");

        cabinet.CabinetName = request.CabinetName.Trim();
        cabinet.RoomId = request.RoomId;
        cabinet.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        cabinet.Status = request.Status;

        await _uow.SaveChangesAsync(ct);

        return await ToResponses(cabinets.QueryNoTracking().Where(c => c.CabinetId == cabinetId)).FirstAsync(ct);
    }

    /// <summary>
    /// Next code for the room: &lt;room code&gt;-T01, -T02, ... (highest existing number + 1).
    /// Matches by code prefix, not by RoomId, so a cabinet later moved to another room still keeps its number taken.
    /// </summary>
    private async Task<string> GenerateCabinetCodeAsync(string roomCode, CancellationToken ct)
    {
        var prefix = $"{roomCode}-T";
        var existingCodes = await _uow.Repository<StorageCabinet>().QueryNoTracking()
            .Where(c => c.CabinetCode.StartsWith(prefix))
            .Select(c => c.CabinetCode)
            .ToListAsync(ct);

        var maxNumber = 0;
        foreach (var code in existingCodes)
        {
            if (int.TryParse(code.Substring(prefix.Length), out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"{prefix}{maxNumber + 1:D2}";
    }

    private async Task EnsureActiveRoomAsync(Guid roomId, CancellationToken ct)
    {
        if (!await _uow.Repository<LabRoom>().QueryNoTracking()
                .AnyAsync(r => r.RoomId == roomId && r.Status == LabRoomStatuses.Active, ct))
            throw new BadRequestException("Phòng lab không tồn tại hoặc đã ngừng sử dụng");
    }

    private static IQueryable<CabinetResponse> ToResponses(IQueryable<StorageCabinet> cabinets) =>
        cabinets.Select(c => new CabinetResponse
        {
            CabinetId = c.CabinetId,
            CabinetCode = c.CabinetCode,
            CabinetName = c.CabinetName,
            RoomId = c.RoomId,
            RoomCode = c.Room.RoomCode,
            RoomName = c.Room.RoomName,
            Location = c.Location,
            Status = c.Status,
            ItemCount = c.ComponentItems.Count(i => i.Status != ComponentItemStatuses.Lost
                                                    && i.Status != ComponentItemStatuses.Retired)
                        + c.ConsumableStocks.Sum(st => st.Quantity),
        });
}

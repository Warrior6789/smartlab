using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class LabRoomService : ILabRoomService
{
    private const string RoomCodePrefix = "LAB-";

    private readonly IUnitOfWork _uow;

    public LabRoomService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IReadOnlyList<LabRoomResponse>> GetLabRoomsAsync(CancellationToken ct = default)
    {
        return await ToResponses(_uow.Repository<LabRoom>().QueryNoTracking()
                .OrderBy(r => r.RoomCode))
            .ToListAsync(ct);
    }

    public async Task<LabRoomResponse> CreateLabRoomAsync(CreateLabRoomRequest request, CancellationToken ct = default)
    {
        var rooms = _uow.Repository<LabRoom>();
        var now = DateTime.UtcNow;
        var room = new LabRoom
        {
            RoomCode = await GenerateRoomCodeAsync(ct),
            RoomName = request.RoomName.Trim(),
            Status = LabRoomStatuses.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await rooms.AddAsync(room, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Có phòng khác vừa được tạo cùng lúc, vui lòng thử lại");
        }

        return await ToResponses(rooms.QueryNoTracking().Where(r => r.RoomId == room.RoomId)).FirstAsync(ct);
    }

    public async Task<LabRoomResponse> UpdateLabRoomAsync(Guid roomId, UpdateLabRoomRequest request, CancellationToken ct = default)
    {
        var rooms = _uow.Repository<LabRoom>();
        var room = await rooms.Query().FirstOrDefaultAsync(r => r.RoomId == roomId, ct)
                   ?? throw new NotFoundException("Không tìm thấy phòng lab");

        if (request.Status == LabRoomStatuses.Inactive && room.Status != LabRoomStatuses.Inactive
            && await _uow.Repository<StorageCabinet>().QueryNoTracking()
                .AnyAsync(c => c.RoomId == roomId && c.Status == CabinetStatuses.Active, ct))
            throw new ConflictException("Phòng còn tủ đang sử dụng, hãy ngừng sử dụng hoặc chuyển các tủ sang phòng khác trước");

        room.RoomName = request.RoomName.Trim();
        room.Status = request.Status;
        room.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync(ct);

        return await ToResponses(rooms.QueryNoTracking().Where(r => r.RoomId == roomId)).FirstAsync(ct);
    }

    /// <summary>Next code: LAB-01, LAB-02, ... (highest existing number + 1).</summary>
    private async Task<string> GenerateRoomCodeAsync(CancellationToken ct)
    {
        var existingCodes = await _uow.Repository<LabRoom>().QueryNoTracking()
            .Where(r => r.RoomCode.StartsWith(RoomCodePrefix))
            .Select(r => r.RoomCode)
            .ToListAsync(ct);

        var maxNumber = 0;
        foreach (var code in existingCodes)
        {
            if (int.TryParse(code.Substring(RoomCodePrefix.Length), out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"{RoomCodePrefix}{maxNumber + 1:D2}";
    }

    private static IQueryable<LabRoomResponse> ToResponses(IQueryable<LabRoom> rooms) =>
        rooms.Select(r => new LabRoomResponse
        {
            RoomId = r.RoomId,
            RoomCode = r.RoomCode,
            RoomName = r.RoomName,
            Status = r.Status,
            CabinetCount = r.StorageCabinets.Count,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
        });
}

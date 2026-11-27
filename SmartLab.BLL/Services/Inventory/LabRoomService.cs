using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class LabRoomService : ILabRoomService
{
    private const string RoomCodePrefix = "LAB-";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public LabRoomService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<LabRoomResponse>> GetLabRoomsAsync(CancellationToken ct = default)
    {
        return await ToResponses(_uow.Repository<LabRoom>().QueryNoTracking()
                .OrderBy(r => EF.Functions.Collate(r.RoomCode, DbCollations.Vietnamese)))
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

    public async Task<IReadOnlyList<LabRoomStaffResponse>> GetStaffAsync(Guid roomId, CancellationToken ct = default)
    {
        var exists = await _uow.Repository<LabRoom>().QueryNoTracking().AnyAsync(r => r.RoomId == roomId, ct);
        if (!exists)
            throw new NotFoundException("Không tìm thấy phòng lab");

        return await LoadStaffAsync(roomId, ct);
    }

    public async Task<IReadOnlyList<LabRoomStaffResponse>> AssignStaffAsync(
        Guid roomId, AssignLabRoomStaffRequest request, CancellationToken ct = default)
    {
        var roomStatus = await _uow.Repository<LabRoom>().QueryNoTracking()
                             .Where(r => r.RoomId == roomId)
                             .Select(r => r.Status)
                             .FirstOrDefaultAsync(ct)
                         ?? throw new NotFoundException("Không tìm thấy phòng lab");
        if (roomStatus != LabRoomStatuses.Active)
            throw new BadRequestException("Phòng lab đã ngừng sử dụng, không phân công nhân viên được");

        var user = await _uow.Repository<User>().QueryNoTracking()
                       .Where(u => u.UserId == request.UserId)
                       .Select(u => new
                       {
                           u.IsActive,
                           IsLabStaff = u.UserRoles.Any(ur => ur.Role.RoleName == RoleNames.LabStaff),
                       })
                       .FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Không tìm thấy người dùng");
        if (!user.IsActive)
            throw new BadRequestException("Tài khoản đã bị khóa, không phân công được");
        if (!user.IsLabStaff)
            throw new BadRequestException("Chỉ phân công được tài khoản có role LabStaff");

        var assignments = _uow.Repository<LabRoomStaff>();
        var assigned = await assignments.QueryNoTracking()
            .AnyAsync(s => s.RoomId == roomId && s.UserId == request.UserId, ct);
        if (assigned)
            throw new ConflictException("Nhân viên đã được phân công ở phòng này");

        var otherRoom = await assignments.QueryNoTracking()
            .Where(s => s.UserId == request.UserId)
            .Select(s => s.Room.RoomName)
            .FirstOrDefaultAsync(ct);
        if (otherRoom != null)
            throw new ConflictException($"Nhân viên đang phụ trách {otherRoom}. Mỗi nhân viên chỉ phụ trách 1 phòng, hãy gỡ khỏi phòng đó trước");

        try
        {
            await assignments.AddAsync(new LabRoomStaff
            {
                RoomId = roomId,
                UserId = request.UserId,
                AssignedAt = DateTime.UtcNow,
            }, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Nhân viên đã được phân công ở phòng này");
        }

        return await LoadStaffAsync(roomId, ct);
    }

    public async Task<IReadOnlyList<LabRoomStaffResponse>> RemoveStaffAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        var removed = await _uow.Repository<LabRoomStaff>().Query()
            .Where(s => s.RoomId == roomId && s.UserId == userId)
            .ExecuteDeleteAsync(ct);
        if (removed == 0)
            throw new NotFoundException("Nhân viên không được phân công ở phòng này");

        return await LoadStaffAsync(roomId, ct);
    }

    private async Task<IReadOnlyList<LabRoomStaffResponse>> LoadStaffAsync(Guid roomId, CancellationToken ct)
    {
        return await _uow.Repository<LabRoomStaff>().QueryNoTracking()
            .Where(s => s.RoomId == roomId)
            .OrderBy(s => EF.Functions.Collate(s.User.FullName, DbCollations.Vietnamese))
            .Select(s => new LabRoomStaffResponse
            {
                UserId = s.UserId,
                Username = s.User.Username,
                FullName = s.User.FullName,
                Email = s.User.Email,
                IsActive = s.User.IsActive,
                AssignedAt = s.AssignedAt,
            })
            .ToListAsync(ct);
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

    public async Task<IReadOnlyList<LabRoomComponentResponse>> GetComponentsAsync(Guid roomId, CancellationToken ct = default)
    {
        if (!await _uow.Repository<LabRoom>().QueryNoTracking().AnyAsync(r => r.RoomId == roomId, ct))
            throw new NotFoundException("Không tìm thấy phòng lab");

        return await _uow.Repository<LabRoomComponent>().QueryNoTracking()
            .Where(l => l.RoomId == roomId)
            .OrderBy(l => EF.Functions.Collate(l.Component.ComponentName, DbCollations.Vietnamese))
            .Select(l => new LabRoomComponentResponse
            {
                ComponentId = l.ComponentId,
                ComponentCode = l.Component.ComponentCode,
                ShortCode = l.Component.ShortCode,
                ComponentName = l.Component.ComponentName,
                CategoryName = l.Component.Category.CategoryName,
                TrackingType = l.Component.TrackingType,
                Unit = l.Component.Unit,
                IsActive = l.Component.IsActive,
                QuantityInLab = l.Component.ConsumableStocks
                                    .Where(st => st.Cabinet.RoomId == roomId)
                                    .Sum(st => st.Quantity)
                                + l.Component.ComponentItems
                                    .Count(i => i.Cabinet.RoomId == roomId
                                                && (i.Status == ComponentItemStatuses.Available || i.Status == ComponentItemStatuses.Borrowed)),
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LabRoomComponentResponse>> UpdateComponentsAsync(
        Guid roomId, UpdateLabRoomComponentsRequest request, CancellationToken ct = default)
    {
        if (!await _uow.Repository<LabRoom>().QueryNoTracking().AnyAsync(r => r.RoomId == roomId, ct))
            throw new NotFoundException("Không tìm thấy phòng lab");

        var wanted = request.ComponentIds.Distinct().ToList();
        var found = await _uow.Repository<Component>().QueryNoTracking()
            .CountAsync(c => wanted.Contains(c.ComponentId), ct);
        if (found != wanted.Count)
            throw new BadRequestException("Có linh kiện không tồn tại");

        var links = _uow.Repository<LabRoomComponent>();
        var current = await links.Query().Where(l => l.RoomId == roomId).ToListAsync(ct);
        var removed = current.Where(l => !wanted.Contains(l.ComponentId)).ToList();

        if (removed.Count > 0)
        {
            var removedIds = removed.Select(l => l.ComponentId).ToList();
            var stillHeld = await _uow.Repository<Component>().QueryNoTracking()
                .Where(c => removedIds.Contains(c.ComponentId)
                            && (c.ConsumableStocks.Any(st => st.Cabinet.RoomId == roomId && st.Quantity > 0)
                                || c.ComponentItems.Any(i => i.Cabinet.RoomId == roomId
                                                             && (i.Status == ComponentItemStatuses.Available
                                                                 || i.Status == ComponentItemStatuses.Borrowed))))
                .OrderBy(c => c.ComponentCode)
                .Select(c => c.ComponentName)
                .ToListAsync(ct);
            if (stillHeld.Count > 0)
                throw new ConflictException(
                    $"Lab còn hàng của {string.Join(", ", stillHeld)} nên chưa bỏ khỏi danh sách được. Thanh lý hết hàng trước khi bỏ");

            foreach (var link in removed)
                links.Remove(link);
        }

        var now = DateTime.UtcNow;
        var currentIds = current.Select(l => l.ComponentId).ToHashSet();
        foreach (var componentId in wanted.Where(id => !currentIds.Contains(id)))
            await links.AddAsync(new LabRoomComponent { RoomId = roomId, ComponentId = componentId, CreatedAt = now }, ct);

        await _uow.SaveChangesAsync(ct);
        return await GetComponentsAsync(roomId, ct);
    }

    private IQueryable<LabRoomResponse> ToResponses(IQueryable<LabRoom> rooms)
    {
        var userId = _currentUser.UserId;
        return rooms.Select(r => new LabRoomResponse
        {
            RoomId = r.RoomId,
            RoomCode = r.RoomCode,
            RoomName = r.RoomName,
            Status = r.Status,
            CabinetCount = r.StorageCabinets.Count,
            StaffCount = r.LabRoomStaffs.Count,
            ComponentCount = r.LabRoomComponents.Count,
            IsMine = r.LabRoomStaffs.Any(s => s.UserId == userId),
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
        });
    }
}

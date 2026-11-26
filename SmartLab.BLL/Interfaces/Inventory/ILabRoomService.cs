using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Interfaces.Inventory;

public interface ILabRoomService
{
    /// <summary>All lab rooms ordered by room code, for lists and dropdowns.</summary>
    Task<IReadOnlyList<LabRoomResponse>> GetLabRoomsAsync(CancellationToken ct = default);

    /// <summary>Creates a room with status Active; the code is generated as LAB-&lt;nn&gt;.</summary>
    Task<LabRoomResponse> CreateLabRoomAsync(CreateLabRoomRequest request, CancellationToken ct = default);

    /// <summary>Updates name and status (the code never changes); cannot set Inactive while it still has Active cabinets.</summary>
    Task<LabRoomResponse> UpdateLabRoomAsync(Guid roomId, UpdateLabRoomRequest request, CancellationToken ct = default);

    /// <summary>LabStaff accounts assigned to the room, ordered by full name.</summary>
    Task<IReadOnlyList<LabRoomStaffResponse>> GetStaffAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>Assigns an active LabStaff account to an Active room; returns the room's staff list.</summary>
    Task<IReadOnlyList<LabRoomStaffResponse>> AssignStaffAsync(Guid roomId, AssignLabRoomStaffRequest request, CancellationToken ct = default);

    /// <summary>Removes a staff assignment from the room; returns the room's staff list.</summary>
    Task<IReadOnlyList<LabRoomStaffResponse>> RemoveStaffAsync(Guid roomId, Guid userId, CancellationToken ct = default);
}

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
}

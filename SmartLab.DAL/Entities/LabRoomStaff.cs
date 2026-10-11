using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class LabRoomStaff
{
    public Guid RoomId { get; set; }

    public Guid UserId { get; set; }

    public DateTime AssignedAt { get; set; }

    public virtual LabRoom Room { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}

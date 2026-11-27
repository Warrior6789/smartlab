using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class LabRoomComponent
{
    public Guid RoomId { get; set; }

    public Guid ComponentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Component Component { get; set; } = null!;

    public virtual LabRoom Room { get; set; } = null!;
}

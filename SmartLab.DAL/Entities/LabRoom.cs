using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class LabRoom
{
    public Guid RoomId { get; set; }

    public string RoomCode { get; set; } = null!;

    public string RoomName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<StorageCabinet> StorageCabinets { get; set; } = new List<StorageCabinet>();
}

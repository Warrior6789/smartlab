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

    public virtual ICollection<BorrowItem> BorrowItems { get; set; } = new List<BorrowItem>();

    public virtual ICollection<BorrowRequest> BorrowRequests { get; set; } = new List<BorrowRequest>();

    public virtual ICollection<LabRoomStaff> LabRoomStaffs { get; set; } = new List<LabRoomStaff>();

    public virtual ICollection<StorageCabinet> StorageCabinets { get; set; } = new List<StorageCabinet>();
}

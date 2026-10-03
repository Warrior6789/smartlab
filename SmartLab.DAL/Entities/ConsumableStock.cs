using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class ConsumableStock
{
    public Guid ComponentId { get; set; }

    public Guid CabinetId { get; set; }

    public int Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual StorageCabinet Cabinet { get; set; } = null!;

    public virtual Component Component { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class ProjectComponent
{
    public Guid ProjectId { get; set; }

    public Guid ComponentId { get; set; }

    public int Quantity { get; set; }

    public string? Note { get; set; }

    public virtual Component Component { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;
}

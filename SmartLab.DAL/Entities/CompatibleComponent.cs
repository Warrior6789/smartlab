using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class CompatibleComponent
{
    public Guid ComponentId { get; set; }

    public Guid CompatibleComponentId { get; set; }

    public string? Note { get; set; }

    public virtual Component CompatibleComponentNavigation { get; set; } = null!;

    public virtual Component Component { get; set; } = null!;
}

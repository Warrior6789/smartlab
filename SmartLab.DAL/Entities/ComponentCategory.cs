using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class ComponentCategory
{
    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Component> Components { get; set; } = new List<Component>();
}

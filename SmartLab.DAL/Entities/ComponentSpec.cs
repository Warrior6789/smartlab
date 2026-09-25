using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class ComponentSpec
{
    public Guid SpecId { get; set; }

    public Guid ComponentId { get; set; }

    public string SpecKey { get; set; } = null!;

    public string SpecValue { get; set; } = null!;

    public string? Unit { get; set; }

    public bool IsAiExtracted { get; set; }

    public decimal? ConfidenceScore { get; set; }

    public virtual Component Component { get; set; } = null!;
}

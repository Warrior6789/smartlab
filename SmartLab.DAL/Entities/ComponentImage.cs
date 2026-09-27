using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class ComponentImage
{
    public Guid ImageId { get; set; }

    public Guid ComponentId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string ImageType { get; set; } = null!;

    public bool IsPrimary { get; set; }

    public string? AnnotationUrl { get; set; }

    public Guid UploadedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? StorageKey { get; set; }

    public virtual Component Component { get; set; } = null!;

    public virtual User UploadedByNavigation { get; set; } = null!;
}

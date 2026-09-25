using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class ClassStudent
{
    public Guid ClassId { get; set; }

    public Guid StudentId { get; set; }

    public DateTime EnrolledAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual Class Class { get; set; } = null!;

    public virtual StudentProfile Student { get; set; } = null!;
}

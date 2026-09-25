using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class InstructorProfile
{
    public Guid InstructorProfileId { get; set; }

    public Guid UserId { get; set; }

    public string InstructorCode { get; set; } = null!;

    public string? Department { get; set; }

    public string? AcademicTitle { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual User User { get; set; } = null!;
}

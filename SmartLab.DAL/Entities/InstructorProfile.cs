using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class InstructorProfile
{
    public Guid InstructorProfileId { get; set; }

    public Guid UserId { get; set; }

    public string InstructorCode { get; set; } = null!;

    public string? AcademicTitle { get; set; }

    public Guid? DepartmentId { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual Department? Department { get; set; }

    public virtual User User { get; set; } = null!;
}

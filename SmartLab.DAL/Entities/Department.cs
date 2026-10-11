using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class Department
{
    public Guid DepartmentId { get; set; }

    public string DepartmentName { get; set; } = null!;

    public Guid? HeadId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? Head { get; set; }

    public virtual ICollection<InstructorProfile> InstructorProfiles { get; set; } = new List<InstructorProfile>();
}

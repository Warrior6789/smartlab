using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class StudentProfile
{
    public Guid StudentProfileId { get; set; }

    public Guid UserId { get; set; }

    public string StudentCode { get; set; } = null!;

    public string? Major { get; set; }

    public string? Cohort { get; set; }

    public virtual ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();

    public virtual User User { get; set; } = null!;
}

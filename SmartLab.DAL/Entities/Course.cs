using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class Course
{
    public Guid CourseId { get; set; }

    public string CourseCode { get; set; } = null!;

    public string CourseName { get; set; } = null!;

    public int Credits { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
}

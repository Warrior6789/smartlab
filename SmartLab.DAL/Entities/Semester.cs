using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class Semester
{
    public Guid SemesterId { get; set; }

    public string SemesterCode { get; set; } = null!;

    public string Term { get; set; } = null!;

    public int Year { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
}

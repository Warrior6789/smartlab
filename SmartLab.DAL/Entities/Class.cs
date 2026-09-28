using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class Class
{
    public Guid ClassId { get; set; }

    public string ClassCode { get; set; } = null!;

    public Guid CourseId { get; set; }

    public Guid SemesterId { get; set; }

    public Guid InstructorId { get; set; }

    public int MaxStudents { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<BorrowRequest> BorrowRequests { get; set; } = new List<BorrowRequest>();

    public virtual ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();

    public virtual Course Course { get; set; } = null!;

    public virtual InstructorProfile Instructor { get; set; } = null!;

    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    public virtual Semester Semester { get; set; } = null!;

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
}

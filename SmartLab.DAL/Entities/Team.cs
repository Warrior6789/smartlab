using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class Team
{
    public Guid TeamId { get; set; }

    public Guid ClassId { get; set; }

    public Guid LeaderId { get; set; }

    public string TeamName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual StudentProfile Leader { get; set; } = null!;

    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}

using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class TeamMember
{
    public Guid TeamId { get; set; }

    public Guid StudentId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime InvitedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public virtual StudentProfile Student { get; set; } = null!;

    public virtual Team Team { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class ComponentTutorial
{
    public Guid TutorialId { get; set; }

    public Guid ComponentId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? VideoUrl { get; set; }

    public Guid AuthorId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User Author { get; set; } = null!;

    public virtual Component Component { get; set; } = null!;
}

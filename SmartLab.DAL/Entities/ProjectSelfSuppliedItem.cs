using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

/// <summary>
/// Linh kien nhom tu mua (khong co trong kho), nhom truong nhap khi tao/sua project
/// </summary>
public partial class ProjectSelfSuppliedItem
{
    public Guid ItemId { get; set; }

    public Guid ProjectId { get; set; }

    public string ItemName { get; set; } = null!;

    public int Quantity { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Project Project { get; set; } = null!;
}

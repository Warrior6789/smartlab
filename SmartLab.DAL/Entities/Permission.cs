using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class Permission
{
    public Guid PermissionId { get; set; }

    public string PermissionCode { get; set; } = null!;

    public string PermissionName { get; set; } = null!;

    public string Module { get; set; } = null!;

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}

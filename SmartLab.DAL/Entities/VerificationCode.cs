using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class VerificationCode
{
    public Guid VerificationCodeId { get; set; }

    public Guid UserId { get; set; }

    public string Purpose { get; set; } = null!;

    public string CodeHash { get; set; } = null!;

    public int AttemptCount { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}

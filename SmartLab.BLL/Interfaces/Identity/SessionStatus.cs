namespace SmartLab.BLL.Interfaces.Identity;

/// <summary>Result of checking a JWT against the account on each request.</summary>
public enum SessionStatus
{
    Valid,

    /// <summary>The account is locked or no longer exists.</summary>
    Locked,

    /// <summary>The token was issued before a logout, password change/reset or role change.</summary>
    Revoked,
}

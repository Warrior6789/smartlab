namespace SmartLab.BLL.Interfaces.Identity;

/// <summary>
/// Checks on every authenticated request that the account is still active, so a locked account stops working
/// before its JWT expires.
/// </summary>
public interface IActiveUserChecker
{
    /// <summary>False for a locked or deleted account. Cached briefly, so a lock takes effect within about a minute.</summary>
    Task<bool> IsActiveAsync(Guid userId, CancellationToken ct = default);
}

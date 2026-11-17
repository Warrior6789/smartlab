namespace SmartLab.BLL.Interfaces.Identity;

/// <summary>
/// Checks on every authenticated request that the account is still active and the token was not revoked,
/// so a locked account or a logged-out token stops working before the JWT expires.
/// </summary>
public interface IActiveUserChecker
{
    /// <summary>
    /// Compares the token's version with users.token_version. Cached briefly; call <see cref="Forget"/> after changing
    /// the version so this server rejects old tokens at once.
    /// </summary>
    Task<SessionStatus> CheckAsync(Guid userId, int tokenVersion, CancellationToken ct = default);

    /// <summary>Drops the cached state of the user so the next request reads it from the database.</summary>
    void Forget(Guid userId);
}

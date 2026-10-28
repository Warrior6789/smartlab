using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartLab.BLL.Interfaces.Identity;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Identity;

public class ActiveUserChecker : IActiveUserChecker
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;

    public ActiveUserChecker(IUnitOfWork uow, IMemoryCache cache)
    {
        _uow = uow;
        _cache = cache;
    }

    public async Task<SessionStatus> CheckAsync(Guid userId, int tokenVersion, CancellationToken ct = default)
    {
        var key = CacheKey(userId);
        if (!_cache.TryGetValue(key, out AccountState? state))
        {
            state = await _uow.Repository<User>().QueryNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new AccountState(u.IsActive, u.TokenVersion))
                .FirstOrDefaultAsync(ct);
            _cache.Set(key, state, CacheDuration);
        }

        if (state == null || !state.IsActive)
            return SessionStatus.Locked;
        if (tokenVersion != state.TokenVersion)
            return SessionStatus.Revoked;

        return SessionStatus.Valid;
    }

    public void Forget(Guid userId) => _cache.Remove(CacheKey(userId));

    private static string CacheKey(Guid userId) => "user-session:" + userId;

    private sealed record AccountState(bool IsActive, int TokenVersion);
}

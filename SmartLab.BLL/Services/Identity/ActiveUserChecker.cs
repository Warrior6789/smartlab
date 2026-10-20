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

    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken ct = default)
    {
        var key = "user-active:" + userId;
        if (_cache.TryGetValue(key, out bool cached))
            return cached;

        var active = await _uow.Repository<User>().QueryNoTracking()
            .AnyAsync(u => u.UserId == userId && u.IsActive, ct);

        _cache.Set(key, active, CacheDuration);
        return active;
    }
}

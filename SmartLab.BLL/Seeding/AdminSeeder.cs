using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Constants;
using SmartLab.BLL.External.Security;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Seeding;

/// <summary>
/// On startup: if no user has the Admin role, create one from SeedAdmin:Email / SeedAdmin:Password
/// (optional SeedAdmin:Username, SeedAdmin:FullName). Never modifies existing users.
/// </summary>
public class AdminSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(IServiceProvider services, IConfiguration configuration, ILogger<AdminSeeder> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await SeedAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin seeding failed");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task SeedAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var adminExists = await uow.Repository<UserRole>().QueryNoTracking()
            .AnyAsync(ur => ur.Role.RoleName == RoleNames.Admin, ct);
        if (adminExists)
            return;

        var email = _configuration["SeedAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = _configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("No Admin user exists and SeedAdmin:Email / SeedAdmin:Password are not configured. Skipping admin seeding.");
            return;
        }

        var username = _configuration["SeedAdmin:Username"]?.Trim() is { Length: > 0 } u ? u : "admin";
        var fullName = _configuration["SeedAdmin:FullName"]?.Trim() is { Length: > 0 } f ? f : "System Administrator";

        var users = uow.Repository<User>();
        if (await users.QueryNoTracking().AnyAsync(x => x.Email.ToLower() == email || x.Username == username, ct))
        {
            _logger.LogWarning("Cannot seed Admin: a user with the configured email or username '{Username}' already exists.", username);
            return;
        }

        var adminRole = await uow.Repository<Role>().QueryNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == RoleNames.Admin, ct);
        if (adminRole is null)
        {
            _logger.LogWarning("Cannot seed Admin: role '{Role}' not found in database.", RoleNames.Admin);
            return;
        }

        var now = DateTime.UtcNow;
        var admin = new User
        {
            Username = username,
            Email = email,
            PasswordHash = hasher.Hash(password),
            FullName = fullName,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        admin.UserRoles.Add(new UserRole { RoleId = adminRole.RoleId, AssignedAt = now });

        await users.AddAsync(admin, ct);
        await uow.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded Admin user '{Username}'.", username);
    }
}

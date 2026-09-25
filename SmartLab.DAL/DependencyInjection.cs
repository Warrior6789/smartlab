using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartLab.DAL.Context;
using SmartLab.DAL.Repositories.Implementations;
using SmartLab.DAL.Repositories.Interfaces;

namespace SmartLab.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Missing ConnectionStrings:DefaultConnection. Set it with: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<value>\" -p SmartLab.API");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<UnitOfWork.IUnitOfWork, UnitOfWork.UnitOfWork>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}

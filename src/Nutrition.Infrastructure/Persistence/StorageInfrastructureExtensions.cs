using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nutrition.Application.Common;

namespace Nutrition.Infrastructure.Persistence;

public static class StorageInfrastructureExtensions
{
    /// <summary>
    /// Registers the configured persistence provider and generic repository services.
    /// </summary>
    /// <remarks>
    /// SQLite is currently the supported provider. PostgreSQL and SQL Server selections
    /// remain compatibility placeholders and intentionally use the SQLite implementation.
    /// </remarks>
    /// <param name="services">The application's dependency-injection service collection.</param>
    /// <param name="configuration">Configuration containing provider and connection settings.</param>
    /// <returns>The same service collection for fluent registration.</returns>
    public static IServiceCollection AddStorageInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";

        switch (provider.ToLowerInvariant())
        {
            case "sqlite":
                services.AddDbContext<DietTrackerDbContext>(options =>
                    options.UseSqlite(configuration.GetConnectionString("DefaultConnection") 
                        ?? "Data Source=diettracker.db"));
                break;

            case "postgresql":
                // Reserved for cloud deployment with Npgsql
                services.AddDbContext<DietTrackerDbContext>(options =>
                    options.UseSqlite(configuration.GetConnectionString("DefaultConnection") 
                        ?? "Data Source=diettracker.db"));
                break;

            case "sqlserver":
                // Reserved for enterprise SQL Server deployment
                services.AddDbContext<DietTrackerDbContext>(options =>
                    options.UseSqlite(configuration.GetConnectionString("DefaultConnection") 
                        ?? "Data Source=diettracker.db"));
                break;

            default:
                services.AddDbContext<DietTrackerDbContext>(options =>
                    options.UseSqlite(configuration.GetConnectionString("DefaultConnection") 
                        ?? "Data Source=diettracker.db"));
                break;
        }

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<Nutrition.Application.Common.Interfaces.IPhotoStorageService, Services.LocalPhotoStorageService>();
        services.AddScoped<Nutrition.Application.Common.Interfaces.ISecretStore, Services.DatabaseSecretStore>();
        services.AddSingleton<Nutrition.Application.Common.Interfaces.IAppEnvironment, Services.AppEnvironment>();
        return services;
    }
}

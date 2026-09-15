using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nutrition.Application.Common;

namespace Nutrition.Infrastructure.Persistence;

public static class StorageInfrastructureExtensions
{
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
        return services;
    }
}

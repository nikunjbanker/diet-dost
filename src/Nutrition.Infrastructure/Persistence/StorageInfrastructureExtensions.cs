/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Infrastructure.Persistence.Services;

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
        var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var provider = !string.IsNullOrWhiteSpace(dbOptions.Provider) ? dbOptions.Provider : "Sqlite";
        var defaultConnStr = configuration.GetConnectionString("DefaultConnection");
        var connectionString = !string.IsNullOrWhiteSpace(defaultConnStr)
            ? defaultConnStr
            : (!string.IsNullOrWhiteSpace(dbOptions.ConnectionString) ? dbOptions.ConnectionString : "Data Source=diettracker.db");

        switch (provider.ToLowerInvariant())
        {
            case "sqlite":
                var builder = new SqliteConnectionStringBuilder(connectionString);
                var useEphemeral = dbOptions.UseEphemeralActiveCache;

                if (useEphemeral && !builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
                {
                    var vaultPath = !string.IsNullOrWhiteSpace(dbOptions.StorageVaultPath) ? dbOptions.StorageVaultPath : builder.DataSource;
                    var activePath = !string.IsNullOrWhiteSpace(dbOptions.ActiveDbPath)
                        ? dbOptions.ActiveDbPath
                        : Path.Combine(Path.GetTempPath(), Path.GetFileName(vaultPath));

                    try
                    {
                        if (File.Exists(vaultPath) && new FileInfo(vaultPath).Length > 0 && !File.Exists(activePath))
                        {
                            var activeDir = Path.GetDirectoryName(activePath);
                            if (!string.IsNullOrWhiteSpace(activeDir))
                            {
                                Directory.CreateDirectory(activeDir);
                            }
                            File.Copy(vaultPath, activePath, overwrite: true);
                        }
                    }
                    catch
                    {
                        // Fallback to fresh creation if initial hydration fails
                    }

                    var activeBuilder = new SqliteConnectionStringBuilder
                    {
                        DataSource = activePath,
                        Cache = SqliteCacheMode.Shared
                    };
                    connectionString = activeBuilder.ToString();

                    services.AddSingleton<ISqliteStorageVaultSyncService>(sp =>
                        new SqliteStorageVaultSyncService(
                            sp.GetRequiredService<ILogger<SqliteStorageVaultSyncService>>(),
                            activePath,
                            vaultPath,
                            enabled: true));
                    services.AddHostedService(sp => (SqliteStorageVaultSyncService)sp.GetRequiredService<ISqliteStorageVaultSyncService>());
                }
                else
                {
                    services.AddSingleton<ISqliteStorageVaultSyncService>(sp =>
                        new SqliteStorageVaultSyncService(
                            sp.GetRequiredService<ILogger<SqliteStorageVaultSyncService>>(),
                            builder.DataSource,
                            null,
                            enabled: false));
                }

                services.AddDbContext<DietTrackerDbContext>(options =>
                    options.UseSqlite(connectionString)
                           .AddInterceptors(new SqlitePragmaInterceptor()));
                break;

            case "postgresql":
                // Reserved for cloud deployment with Npgsql
                services.AddDbContext<DietTrackerDbContext>(options =>
                    options.UseSqlite(connectionString));
                break;

            case "sqlserver":
                // Reserved for enterprise SQL Server deployment
                services.AddDbContext<DietTrackerDbContext>(options =>
                    options.UseSqlite(connectionString));
                break;

            default:
                services.AddDbContext<DietTrackerDbContext>(options =>
                    options.UseSqlite(connectionString));
                break;
        }

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<Nutrition.Application.Common.Interfaces.IPhotoStorageService, Nutrition.Infrastructure.Services.LocalPhotoStorageService>();
        services.AddScoped<Nutrition.Application.Common.Interfaces.ISecretStore, Nutrition.Infrastructure.Services.DatabaseSecretStore>();
        services.AddSingleton<Nutrition.Application.Common.Interfaces.IAppEnvironment, Nutrition.Infrastructure.Services.AppEnvironment>();
        return services;
    }
}

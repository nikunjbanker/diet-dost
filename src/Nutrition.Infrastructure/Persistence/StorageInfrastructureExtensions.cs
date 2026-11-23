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
        var provider = !string.IsNullOrWhiteSpace(dbOptions.Provider) ? dbOptions.Provider : "AzureSql";
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

            case "azuresql":
            case "sqlserver":
                var sqlConnectionString = configuration.GetConnectionString("AzureSqlConnection")
                    ?? (!string.IsNullOrWhiteSpace(defaultConnStr) && !defaultConnStr.Contains(".db", StringComparison.OrdinalIgnoreCase) ? defaultConnStr : null);

                if (string.IsNullOrWhiteSpace(sqlConnectionString) &&
                    !string.IsNullOrWhiteSpace(dbOptions.ConnectionString) &&
                    !dbOptions.ConnectionString.Contains(".db", StringComparison.OrdinalIgnoreCase))
                {
                    sqlConnectionString = dbOptions.ConnectionString;
                }

                if (string.IsNullOrWhiteSpace(sqlConnectionString))
                {
                    throw new InvalidOperationException(
                        "Database provider is configured as 'AzureSql' or 'SqlServer', but no connection string was found. " +
                        "Please configure 'ConnectionStrings:AzureSqlConnection' or configure Azure Key Vault secret 'azure-sql-connection-string'.");
                }

                // Guarantee minimum 120-second connection timeout for Azure SQL Serverless cold-pause wake-ups (resuming takes 15-45s)
                try
                {
                    var sqlBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sqlConnectionString);
                    if (sqlBuilder.ConnectTimeout < 120)
                    {
                        sqlBuilder.ConnectTimeout = 120;
                        sqlConnectionString = sqlBuilder.ConnectionString;
                    }
                }
                catch
                {
                    // Fall back to raw connection string if custom or non-standard format
                }

                services.AddDbContext<DietTrackerDbContext>(options =>
                {
                    options.UseSqlServer(sqlConnectionString, sqlOpts =>
                    {
                        sqlOpts.EnableRetryOnFailure(
                            maxRetryCount: 6,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: new[] { 40613, 258, -2 }); // Azure SQL Serverless auto-pause waking and timeout errors
                        sqlOpts.CommandTimeout(120);
                        sqlOpts.MigrationsAssembly(typeof(DietTrackerDbContext).Assembly.FullName);
                    });
                });
                break;

            case "documentdb":
            case "mongodb":
                var docDbConn = configuration.GetConnectionString("DocumentDbConnection")
                    ?? (!string.IsNullOrWhiteSpace(defaultConnStr) && defaultConnStr.StartsWith("mongodb", StringComparison.OrdinalIgnoreCase) ? defaultConnStr : null)
                    ?? (!string.IsNullOrWhiteSpace(dbOptions.ConnectionString) && dbOptions.ConnectionString.StartsWith("mongodb", StringComparison.OrdinalIgnoreCase) ? dbOptions.ConnectionString : null)
                    ?? "mongodb://localhost:27017";

                var docDbOptions = configuration.GetSection(Nutrition.Infrastructure.Persistence.DocumentDb.DocumentDbOptions.SectionName).Get<Nutrition.Infrastructure.Persistence.DocumentDb.DocumentDbOptions>() ?? new Nutrition.Infrastructure.Persistence.DocumentDb.DocumentDbOptions();

                Nutrition.Infrastructure.Persistence.DocumentDb.DocumentDbBsonMappings.Register();

                var mongoUrl = new MongoDB.Driver.MongoUrl(docDbConn);
                var mongoSettings = MongoDB.Driver.MongoClientSettings.FromUrl(mongoUrl);
                mongoSettings.ConnectTimeout = TimeSpan.FromMilliseconds(docDbOptions.ConnectTimeoutMs);
                mongoSettings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(docDbOptions.ServerSelectionTimeoutMs);
                mongoSettings.MaxConnectionIdleTime = TimeSpan.FromMilliseconds(docDbOptions.MaxConnectionIdleTimeMs);

                var mongoClient = new MongoDB.Driver.MongoClient(mongoSettings);
                var targetDbName = !string.IsNullOrWhiteSpace(mongoUrl.DatabaseName) ? mongoUrl.DatabaseName : docDbOptions.DatabaseName;
                var mongoDatabase = mongoClient.GetDatabase(targetDbName);

                services.AddSingleton<MongoDB.Driver.IMongoClient>(mongoClient);
                services.AddSingleton<MongoDB.Driver.IMongoDatabase>(mongoDatabase);

                // Register resilient fallback DbContext with guaranteed schema so any legacy EF consumer can resolve without missing table errors
                var fallbackDbPath = Path.Combine(Path.GetTempPath(), "diet_dost_docdb_fallback.db");
                try
                {
                    var fallbackBuilder = new DbContextOptionsBuilder<DietTrackerDbContext>()
                        .UseSqlite($"Data Source={fallbackDbPath}");
                    using var fallbackContext = new DietTrackerDbContext(fallbackBuilder.Options);
                    fallbackContext.Database.EnsureCreated();
                }
                catch
                {
                    // Non-fatal fallback schema initialization
                }

                services.AddDbContext<DietTrackerDbContext>(options =>
                    options.UseSqlite($"Data Source={fallbackDbPath}"));

                services.AddScoped(typeof(IRepository<>), typeof(Nutrition.Infrastructure.Persistence.DocumentDb.DocumentDbRepository<>));
                services.AddScoped<IUnitOfWork, Nutrition.Infrastructure.Persistence.DocumentDb.DocumentDbUnitOfWork>();
                break;

            default:
                services.AddDbContext<DietTrackerDbContext>(options =>
                    options.UseSqlite(connectionString));
                services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
                services.AddScoped<IUnitOfWork, EfUnitOfWork>();
                break;
        }

        if (!provider.Equals("documentdb", StringComparison.OrdinalIgnoreCase) && !provider.Equals("mongodb", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        }

        services.AddScoped<IDatabaseWarmupService, DatabaseWarmupService>();
        services.AddScoped<Nutrition.Application.Common.Interfaces.IPhotoStorageService, Nutrition.Infrastructure.Services.LocalPhotoStorageService>();
        services.AddScoped<Nutrition.Application.Common.Interfaces.ISecretStore, Nutrition.Infrastructure.Services.DatabaseSecretStore>();
        services.AddSingleton<Nutrition.Application.Common.Interfaces.IAppEnvironment, Nutrition.Infrastructure.Services.AppEnvironment>();
        return services;
    }
}

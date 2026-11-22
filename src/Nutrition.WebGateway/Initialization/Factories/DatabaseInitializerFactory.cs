/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Nutrition.Application.Common.Options;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Initialization.Abstractions;
using Nutrition.WebGateway.Initialization.Initializers;

namespace Nutrition.WebGateway.Initialization.Factories;

/// <summary>
/// Factory resolving the active database initializer strategy based on the configured persistence provider.
/// </summary>
public sealed class DatabaseInitializerFactory : IDatabaseInitializerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseOptions _options;

    public DatabaseInitializerFactory(IServiceProvider serviceProvider, IOptions<DatabaseOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? new DatabaseOptions();
    }

    public IDatabaseInitializer GetInitializer(string? providerName = null)
    {
        var targetProvider = (providerName ?? _options.Provider ?? string.Empty).Trim().ToLowerInvariant();

        switch (targetProvider)
        {
            case "documentdb":
            case "mongodb":
                var mongoDb = _serviceProvider.GetService<IMongoDatabase>();
                if (mongoDb != null)
                {
                    var docDbLogger = _serviceProvider.GetRequiredService<ILogger<DocumentDbDatabaseInitializer>>();
                    return new DocumentDbDatabaseInitializer(mongoDb, docDbLogger);
                }
                throw new InvalidOperationException("Configured database provider is DocumentDb, but IMongoDatabase service is not registered in DI container.");

            case "azuresql":
            case "sqlserver":
                var sqlDb = _serviceProvider.GetRequiredService<DietTrackerDbContext>();
                var sqlLogger = _serviceProvider.GetRequiredService<ILogger<AzureSqlDatabaseInitializer>>();
                return new AzureSqlDatabaseInitializer(sqlDb, sqlLogger);

            case "sqlite":
                var sqliteDb = _serviceProvider.GetRequiredService<DietTrackerDbContext>();
                var sqliteLogger = _serviceProvider.GetRequiredService<ILogger<SqliteDatabaseInitializer>>();
                return new SqliteDatabaseInitializer(sqliteDb, sqliteLogger);

            default:
                // Auto-detect from registered DbContext if provider name is unrecognized
                var fallbackDb = _serviceProvider.GetService<DietTrackerDbContext>();
                if (fallbackDb != null)
                {
                    if (fallbackDb.Database.IsSqlServer())
                    {
                        var logger = _serviceProvider.GetRequiredService<ILogger<AzureSqlDatabaseInitializer>>();
                        return new AzureSqlDatabaseInitializer(fallbackDb, logger);
                    }
                    var defaultLogger = _serviceProvider.GetRequiredService<ILogger<SqliteDatabaseInitializer>>();
                    return new SqliteDatabaseInitializer(fallbackDb, defaultLogger);
                }
                throw new NotSupportedException($"Unsupported database persistence provider: '{providerName ?? _options.Provider}'.");
        }
    }
}

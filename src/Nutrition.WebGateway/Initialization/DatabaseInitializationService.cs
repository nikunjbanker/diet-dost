/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Nutrition.WebGateway.Initialization.Abstractions;

namespace Nutrition.WebGateway.Initialization;

/// <summary>
/// Authoritative initialization coordinator service orchestrating provider-specific schema initialization
/// and ordered data seeders across SQLite, Azure SQL, and Azure DocumentDB.
/// </summary>
public sealed class DatabaseInitializationService
{
    private readonly IDatabaseInitializerFactory _initializerFactory;
    private readonly IEnumerable<IDatabaseSeeder> _seeders;
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(
        IDatabaseInitializerFactory initializerFactory,
        IEnumerable<IDatabaseSeeder> seeders,
        ILogger<DatabaseInitializationService> logger)
    {
        _initializerFactory = initializerFactory ?? throw new ArgumentNullException(nameof(initializerFactory));
        _seeders = seeders ?? throw new ArgumentNullException(nameof(seeders));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAndSeedAsync(CancellationToken cancellationToken = default)
    {
        var initializer = _initializerFactory.GetInitializer();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[INIT] Selected database initializer strategy: '{ProviderName}'.", initializer.ProviderName);
        }

        await initializer.InitializeSchemaAsync(cancellationToken);

        var orderedSeeders = _seeders.OrderBy(s => s.Order).ToList();
        foreach (var seeder in orderedSeeders)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("[SEED] Executing seeder '{SeederType}' (Order: {Order})...", seeder.GetType().Name, seeder.Order);
            }
            await seeder.SeedAsync(cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("[INIT] Database schema initialization and data seeding completed successfully across all providers.");
        }
    }
}

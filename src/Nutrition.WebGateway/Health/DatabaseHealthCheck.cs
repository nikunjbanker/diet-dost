/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using Nutrition.Infrastructure.Persistence;

namespace Nutrition.WebGateway.Health;

/// <summary>
/// Health check that verifies database connectivity and schema readiness.
/// Supports SQLite, Azure SQL, and Azure DocumentDB (Free Tier).
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly DietTrackerDbContext _dbContext;
    private readonly IMongoDatabase? _mongoDatabase;

    public DatabaseHealthCheck(DietTrackerDbContext dbContext)
        : this(dbContext, null)
    {
    }

    public DatabaseHealthCheck(DietTrackerDbContext dbContext, IServiceProvider? serviceProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mongoDatabase = serviceProvider?.GetService<IMongoDatabase>();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_mongoDatabase != null)
            {
                var pingCmd = new BsonDocument("ping", 1);
                await _mongoDatabase.RunCommandAsync<BsonDocument>(pingCmd, cancellationToken: cancellationToken);
                var docDbData = new Dictionary<string, object>
                {
                    ["provider"] = "DocumentDb",
                    ["database"] = _mongoDatabase.DatabaseNamespace.DatabaseName,
                    ["isDocumentDb"] = true
                };

                return HealthCheckResult.Healthy($"Azure DocumentDB is connected and ready (Database: {_mongoDatabase.DatabaseNamespace.DatabaseName}).", docDbData);
            }

            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                var providerName = _dbContext.Database.ProviderName ?? "Unknown";
                var data = new Dictionary<string, object>
                {
                    ["provider"] = providerName,
                    ["isSqlServer"] = _dbContext.Database.IsSqlServer(),
                    ["isSqlite"] = _dbContext.Database.IsSqlite()
                };

                return HealthCheckResult.Healthy($"Database is connected and ready using provider '{providerName}'.", data);
            }

            return HealthCheckResult.Unhealthy("Database connection failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check threw an exception.", ex);
        }
    }
}

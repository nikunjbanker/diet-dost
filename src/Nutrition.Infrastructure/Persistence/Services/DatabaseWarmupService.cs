/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nutrition.Application.Common.Interfaces;

namespace Nutrition.Infrastructure.Persistence.Services;

/// <summary>
/// Pre-warms the active EF Core persistence layer by sending a lightweight query (e.g. SELECT 1; or CanConnect).
/// For Azure SQL Serverless Free Tier, this triggers the cloud instance to resume from paused state
/// before user-facing write or query operations take place.
/// </summary>
public class DatabaseWarmupService : IDatabaseWarmupService
{
    private readonly DietTrackerDbContext _dbContext;
    private readonly ILogger<DatabaseWarmupService> _logger;

    public DatabaseWarmupService(
        DietTrackerDbContext dbContext,
        ILogger<DatabaseWarmupService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DatabaseWarmupResult> PingAndWarmupAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var provider = _dbContext.Database.ProviderName ?? "Unknown";

        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[WARMUP] Probing database persistence via provider '{Provider}'...", provider);
            }

            if (_dbContext.Database.IsSqlServer())
            {
                // ExecuteSqlRawAsync executes SELECT 1 directly against Azure SQL / SQL Server
                // triggering instance resumption if paused.
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1;", ct);
            }
            else
            {
                // SQLite or other in-memory / file databases
                await _dbContext.Database.CanConnectAsync(ct);
            }

            sw.Stop();
            var latency = sw.ElapsedMilliseconds;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[WARMUP] Database probe succeeded. Provider: {Provider}, Latency: {LatencyMs}ms", provider, latency);
            }

            return new DatabaseWarmupResult(
                IsOnline: true,
                DatabaseProvider: provider,
                LatencyMs: latency,
                Message: "Database is active and ready."
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            var latency = sw.ElapsedMilliseconds;

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "[WARMUP] Database warm-up probe encountered exception after {LatencyMs}ms. Instance may be waking from serverless pause: {ErrorMessage}", latency, ex.Message);
            }

            return new DatabaseWarmupResult(
                IsOnline: false,
                DatabaseProvider: provider,
                LatencyMs: latency,
                Message: $"Database waking: {ex.Message}"
            );
        }
    }
}

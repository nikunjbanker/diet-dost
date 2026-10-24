/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Health;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenConnected_IncludesProviderNameInHealthData()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new DietTrackerDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("provider"), "Expected 'provider' key in health check data dictionary.");
        Assert.NotNull(result.Data["provider"]);
        Assert.True(result.Data.ContainsKey("isSqlServer"), "Expected 'isSqlServer' key in health check data dictionary.");
        Assert.True(result.Data.ContainsKey("isSqlite"), "Expected 'isSqlite' key in health check data dictionary.");
    }
}

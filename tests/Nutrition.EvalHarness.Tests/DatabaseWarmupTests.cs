/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Features.SystemWarmup.Queries.WarmupDatabase;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Persistence.Services;
using Nutrition.WebGateway.Controllers;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class DatabaseWarmupTests
{
    private class FakeDatabaseWarmupService : IDatabaseWarmupService
    {
        private readonly DatabaseWarmupResult _result;

        public FakeDatabaseWarmupService(DatabaseWarmupResult result)
        {
            _result = result;
        }

        public Task<DatabaseWarmupResult> PingAndWarmupAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_result);
        }
    }

    private class FakeDispatcher : IDispatcher
    {
        private readonly object _result;

        public FakeDispatcher(object result)
        {
            _result = result;
        }

        public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
            Task.FromResult((TResult)_result);

        public Task<Result> SendAsync(ICommand command, CancellationToken ct = default) =>
            Task.FromResult((Result)_result);

        public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
            Task.FromResult((TResult)_result);
    }

    [Fact]
    public async Task WarmupDatabaseQueryHandler_WhenDatabaseIsOnline_ReturnsSuccessResult()
    {
        // Arrange
        var fakeResult = new DatabaseWarmupResult(
            IsOnline: true,
            DatabaseProvider: "Microsoft.EntityFrameworkCore.SqlServer",
            LatencyMs: 14,
            Message: "Database is active and ready."
        );
        var fakeService = new FakeDatabaseWarmupService(fakeResult);
        var handler = new WarmupDatabaseQueryHandler(fakeService);

        // Act
        var result = await handler.HandleAsync(new WarmupDatabaseQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsOnline);
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", result.Data.DatabaseProvider);
        Assert.Equal(14, result.Data.LatencyMs);
        Assert.Equal("Database is active and ready.", result.Data.Message);
    }

    [Fact]
    public async Task WarmupDatabaseQueryHandler_WhenDatabaseIsWaking_ReturnsWakingStatusGracefully()
    {
        // Arrange
        var fakeResult = new DatabaseWarmupResult(
            IsOnline: false,
            DatabaseProvider: "Microsoft.EntityFrameworkCore.SqlServer",
            LatencyMs: 4210,
            Message: "Database waking: Database is currently auto-resuming."
        );
        var fakeService = new FakeDatabaseWarmupService(fakeResult);
        var handler = new WarmupDatabaseQueryHandler(fakeService);

        // Act
        var result = await handler.HandleAsync(new WarmupDatabaseQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsOnline);
        Assert.Contains("auto-resuming", result.Data.Message);
    }

    [Fact]
    public async Task DatabaseWarmupService_WithSqlitePersistence_ExecutesCanConnectSuccessfully()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DietTrackerDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new DietTrackerDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var service = new DatabaseWarmupService(dbContext, NullLogger<DatabaseWarmupService>.Instance);

        // Act
        var warmupResult = await service.PingAndWarmupAsync(CancellationToken.None);

        // Assert
        Assert.True(warmupResult.IsOnline);
        Assert.Contains("Sqlite", warmupResult.DatabaseProvider);
        Assert.True(warmupResult.LatencyMs >= 0);
        Assert.Equal("Database is active and ready.", warmupResult.Message);
    }

    [Fact]
    public async Task SystemController_WarmupDatabase_DispatchesQueryAndReturnsOk()
    {
        // Arrange
        var warmupData = new DatabaseWarmupResult(
            IsOnline: true,
            DatabaseProvider: "Sqlite",
            LatencyMs: 5,
            Message: "Database is active and ready."
        );
        var fakeDispatcher = new FakeDispatcher(Result<DatabaseWarmupResult>.Success(warmupData));
        var controller = new SystemController(fakeDispatcher);

        // Act
        var response = await controller.WarmupDatabase(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(response);
        var data = Assert.IsType<DatabaseWarmupResult>(okResult.Value);
        Assert.True(data.IsOnline);
        Assert.Equal(5, data.LatencyMs);
    }

    [Fact]
    public void ProgressMediaAssets_AreValidObsidianBiometricIllustrations()
    {
        // Locate wwwroot progress and placeholder assets
        var baseDir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(baseDir);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "DietDost.slnx")) && !File.Exists(Path.Combine(current.FullName, "DietDost.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        var repoRoot = current.FullName;
        var progressDir = Path.Combine(repoRoot, "src", "Nutrition.WebGateway", "wwwroot", "uploads", "progress");
        var assetsDir = Path.Combine(repoRoot, "src", "Nutrition.WebGateway", "wwwroot", "assets");

        var requiredFiles = new[]
        {
            Path.Combine(progressDir, "face_baseline.svg"),
            Path.Combine(progressDir, "face_current.svg"),
            Path.Combine(progressDir, "body_baseline.svg"),
            Path.Combine(progressDir, "body_current.svg"),
            Path.Combine(assetsDir, "placeholder-progress.svg")
        };

        foreach (var filePath in requiredFiles)
        {
            Assert.True(File.Exists(filePath), $"Expected asset file to exist at '{filePath}'");
            var content = File.ReadAllText(filePath);
            Assert.StartsWith("<svg", content.TrimStart());
            Assert.EndsWith("</svg>", content.TrimEnd());
            Assert.True(content.Length > 500, $"Asset '{Path.GetFileName(filePath)}' should be a rich high-fidelity illustration.");
            Assert.DoesNotContain("👤", content);
        }
    }
}

/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Persistence.DocumentDb;
using Nutrition.WebGateway.Health;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Unit and integration contract tests for Azure DocumentDB (Free Tier - 32 GB dedicated storage)
/// provider configuration, BSON class mappings, generic repository resolution, and health check.
/// </summary>
public class DocumentDbPersistenceTests
{
    private const string SampleDocDbConnection = "mongodb://dietdost-admin:Secr3tP%40ssw0rd@docdb-dietdost-free.documents.azure.com:10255/dietdost?ssl=true&replicaSet=globaldb&retrywrites=false&maxIdleTimeMS=120000";

    [Fact]
    public void BsonMappings_Register_ConfiguresClassMapsForCoreDomainEntities()
    {
        // Act
        DocumentDbBsonMappings.Register();

        // Assert - verify key domain entities have registered BsonClassMap
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(ApplicationUser)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(UserProfile)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(MealLog)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(FoodItemRecord)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(DailyCalorieLedger)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(TierFeatureConfiguration)));

        // Verify ApplicationUser ID member is mapped as IdMember
        var userMap = BsonClassMap.LookupClassMap(typeof(ApplicationUser));
        Assert.NotNull(userMap.IdMemberMap);
        Assert.Equal("Id", userMap.IdMemberMap.MemberName);

        // Calling Register multiple times must be idempotent and not throw
        var exception = Record.Exception(() => DocumentDbBsonMappings.Register());
        Assert.Null(exception);
    }

    [Fact]
    public void AddStorageInfrastructure_ConfiguredWithDocumentDb_RegistersMongoServicesAndRepository()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "DocumentDb",
            ["ConnectionStrings:DocumentDbConnection"] = SampleDocDbConnection,
            ["DocumentDb:DatabaseName"] = "dietdost_test",
            ["DocumentDb:ConnectTimeoutMs"] = "30000"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStorageInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Assert - Mongo client and database are registered
        var mongoClient = scope.ServiceProvider.GetService<IMongoClient>();
        var mongoDatabase = scope.ServiceProvider.GetService<IMongoDatabase>();
        Assert.NotNull(mongoClient);
        Assert.NotNull(mongoDatabase);
        Assert.Equal("dietdost", mongoDatabase.DatabaseNamespace.DatabaseName);

        // Assert - Generic repository resolves to DocumentDbRepository
        var userRepo = scope.ServiceProvider.GetRequiredService<IRepository<ApplicationUser>>();
        Assert.NotNull(userRepo);
        Assert.IsType<DocumentDbRepository<ApplicationUser>>(userRepo);

        // Assert - Unit of work resolves to DocumentDbUnitOfWork
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        Assert.NotNull(uow);
        Assert.IsType<DocumentDbUnitOfWork>(uow);
    }

    [Fact]
    public void AddStorageInfrastructure_ConfiguredWithMongoDbAlias_ResolvesToDocumentDb()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "mongodb",
            ["ConnectionStrings:DefaultConnection"] = SampleDocDbConnection
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStorageInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var userRepo = scope.ServiceProvider.GetRequiredService<IRepository<UserProfile>>();
        Assert.NotNull(userRepo);
        Assert.IsType<DocumentDbRepository<UserProfile>>(userRepo);
    }

    [Fact]
    public void DocumentDbOptions_Defaults_MatchAzureFreeTierBestPractices()
    {
        var options = new DocumentDbOptions();

        Assert.Equal("dietdost", options.DatabaseName);
        Assert.Equal(60000, options.ConnectTimeoutMs); // Minimum 60s for cold wake up
        Assert.Equal(60000, options.ServerSelectionTimeoutMs);
        Assert.Equal(120000, options.MaxConnectionIdleTimeMs);
        Assert.Equal(32, options.AllocatedStorageGb); // 32 GB Free Dedicated Storage
        Assert.True(options.AutoPauseResilienceEnabled);
    }

    [Fact]
    public async Task DatabaseHealthCheck_WhenDocumentDbUnreachable_ReturnsUnhealthy()
    {
        // Arrange
        var services = new ServiceCollection();
        var client = new MongoClient("mongodb://127.0.0.1:19999?serverSelectionTimeoutMS=500&connectTimeoutMS=500");
        var database = client.GetDatabase("dietdost_test");
        services.AddSingleton<IMongoDatabase>(database);
        var serviceProvider = services.BuildServiceProvider();

        var dbContextOptions = new DbContextOptionsBuilder<DietTrackerDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var dbContext = new DietTrackerDbContext(dbContextOptions);

        var healthCheck = new DatabaseHealthCheck(dbContext, serviceProvider);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert - verifies DocumentDb branch was entered and handled connection failure safely
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Database health check threw an exception", result.Description);
    }

    [Fact]
    public void ResilientDateOnlySerializer_DeserializesNonZeroTimeComponentsAndStrings_WithoutException()
    {
        DocumentDbBsonMappings.Register();

        // 1. Simulate BsonDateTime with non-zero time component (e.g. 18:30:00 UTC from IST offset)
        var shiftedDt = new DateTime(2026, 11, 23, 18, 30, 45, DateTimeKind.Utc);
        var bsonDoc = new BsonDocument
        {
            ["_id"] = "ledger-test-dateonly",
            ["UserId"] = "usr-42",
            ["Date"] = new BsonDateTime(shiftedDt),
            ["BudgetedCalories"] = 1800.0,
            ["ConsumedCalories"] = 650.0
        };

        // Act - deserialize DailyCalorieLedger
        var ledger = BsonSerializer.Deserialize<DailyCalorieLedger>(bsonDoc);

        // Assert
        Assert.NotNull(ledger);
        Assert.Equal(new DateOnly(2026, 11, 23), ledger.Date);
        Assert.Equal("usr-42", ledger.UserId);
        Assert.Equal(1800.0, ledger.BudgetedCalories);

        // 2. Simulate ISO string date
        var stringDateDoc = new BsonDocument
        {
            ["_id"] = "ledger-test-string",
            ["UserId"] = "usr-43",
            ["Date"] = "2026-11-25",
            ["BudgetedCalories"] = 2000.0
        };

        var stringLedger = BsonSerializer.Deserialize<DailyCalorieLedger>(stringDateDoc);
        Assert.NotNull(stringLedger);
        Assert.Equal(new DateOnly(2026, 11, 25), stringLedger.Date);
    }
}

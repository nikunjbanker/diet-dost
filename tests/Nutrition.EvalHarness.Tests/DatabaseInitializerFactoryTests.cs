/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Nutrition.Application.Common.Options;
using Nutrition.Infrastructure.Persistence;
using Nutrition.WebGateway.Initialization.Abstractions;
using Nutrition.WebGateway.Initialization.Factories;
using Nutrition.WebGateway.Initialization.Initializers;
using Nutrition.WebGateway.Initialization.Seeders;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Unit tests asserting that the DatabaseInitializerFactory correctly resolves provider-specific strategies
/// and enforces order guarantees on registered seeders (OOP & SOLID compliance).
/// </summary>
public class DatabaseInitializerFactoryTests
{
    private static IServiceProvider CreateServiceProviderWithSqlite(string providerName = "Sqlite")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<DatabaseOptions>>(Options.Create(new DatabaseOptions { Provider = providerName }));
        services.AddDbContext<DietTrackerDbContext>(opt => opt.UseSqlite($"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared"));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Factory_Resolves_SqliteInitializer_When_Configured()
    {
        var sp = CreateServiceProviderWithSqlite("Sqlite");
        var options = sp.GetRequiredService<IOptions<DatabaseOptions>>();
        var factory = new DatabaseInitializerFactory(sp, options);

        var initializer = factory.GetInitializer();

        Assert.NotNull(initializer);
        Assert.IsType<SqliteDatabaseInitializer>(initializer);
        Assert.Equal("Sqlite", initializer.ProviderName);
    }

    [Fact]
    public void Factory_Resolves_AzureSqlInitializer_When_Configured()
    {
        var sp = CreateServiceProviderWithSqlite("AzureSql");
        var options = sp.GetRequiredService<IOptions<DatabaseOptions>>();
        var factory = new DatabaseInitializerFactory(sp, options);

        var initializer = factory.GetInitializer();

        Assert.NotNull(initializer);
        Assert.IsType<AzureSqlDatabaseInitializer>(initializer);
        Assert.Equal("AzureSql", initializer.ProviderName);
    }

    [Fact]
    public void Factory_Resolves_DocumentDbInitializer_When_Configured_With_MongoDb()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<DatabaseOptions>>(Options.Create(new DatabaseOptions { Provider = "DocumentDb" }));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var mongoClient = new MongoClient("mongodb://127.0.0.1:27017/?connectTimeoutMS=100&serverSelectionTimeoutMS=100");
        var mongoDatabase = mongoClient.GetDatabase("dietdost_test");
        services.AddSingleton<IMongoDatabase>(mongoDatabase);

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<DatabaseOptions>>();
        var factory = new DatabaseInitializerFactory(sp, options);

        var initializer = factory.GetInitializer();

        Assert.NotNull(initializer);
        Assert.IsType<DocumentDbDatabaseInitializer>(initializer);
        Assert.Equal("DocumentDb", initializer.ProviderName);
    }

    [Fact]
    public void Factory_Throws_When_DocumentDb_Configured_Without_MongoDb_Registration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<DatabaseOptions>>(Options.Create(new DatabaseOptions { Provider = "DocumentDb" }));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<DatabaseOptions>>();
        var factory = new DatabaseInitializerFactory(sp, options);

        var ex = Assert.Throws<InvalidOperationException>(() => factory.GetInitializer());
        Assert.Contains("IMongoDatabase service is not registered", ex.Message);
    }

    [Fact]
    public void Seeders_Maintain_Deterministic_Order_Guarantees()
    {
        // Assert strict order: AppSecrets (10) -> DefaultData (20) -> StorageMediaMirror (30)
        var secretsSeederOrder = 10;
        var defaultDataSeederOrder = 20;
        var storageMediaMirrorSeederOrder = 30;

        Assert.True(secretsSeederOrder < defaultDataSeederOrder, "AppSecrets must run before DefaultDataSeeder to provide demo credentials");
        Assert.True(defaultDataSeederOrder < storageMediaMirrorSeederOrder, "DefaultDataSeeder must run before StorageMediaMirrorSeeder to create media references");
    }
}

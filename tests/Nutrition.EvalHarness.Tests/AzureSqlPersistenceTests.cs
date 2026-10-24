/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nutrition.Application.Common.Options;
using Nutrition.Infrastructure.Persistence;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

public class AzureSqlPersistenceTests
{
    private const string SampleAzureSqlConnection =
        "Server=tcp:sql-dietdost-prod.database.windows.net,1433;Database=dietdost;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;";

    [Fact]
    public void AddStorageInfrastructure_ConfiguredWithSqlServer_RegistersSqlServerDbContext()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SqlServer",
            ["ConnectionStrings:AzureSqlConnection"] = SampleAzureSqlConnection
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStorageInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();

        Assert.NotNull(db);
        Assert.True(db.Database.IsSqlServer(), "Expected DietTrackerDbContext to be configured with Microsoft.EntityFrameworkCore.SqlServer.");
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);
    }

    [Fact]
    public void AddStorageInfrastructure_ConfiguredWithAzureSqlAlias_RegistersSqlServerDbContext()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "AzureSql",
            ["ConnectionStrings:DefaultConnection"] = SampleAzureSqlConnection
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStorageInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();

        Assert.NotNull(db);
        Assert.True(db.Database.IsSqlServer(), "Expected 'AzureSql' provider alias to resolve to SQL Server provider.");
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);
    }

    [Fact]
    public void AddStorageInfrastructure_SqlServer_ConfiguresAutoPauseExecutionStrategy()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "AzureSql",
            ["ConnectionStrings:AzureSqlConnection"] = SampleAzureSqlConnection
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStorageInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();

        var strategy = db.Database.CreateExecutionStrategy();
        Assert.NotNull(strategy);
        Assert.Equal("SqlServerRetryingExecutionStrategy", strategy.GetType().Name);
    }

    [Theory]
    [InlineData("AzureSql")]
    [InlineData("SqlServer")]
    [InlineData("azuresql")]
    [InlineData("SQLSERVER")]
    public void DatabaseOptions_BindsAzureSqlAliasesCorrectly(string providerName)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Database:Provider"] = providerName,
            ["Database:ConnectionString"] = SampleAzureSqlConnection
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>();

        Assert.NotNull(options);
        Assert.Equal(providerName, options.Provider);
        Assert.Equal(SampleAzureSqlConnection, options.ConnectionString);
    }

    [Fact]
    public void AddStorageInfrastructure_DefaultFallback_RetainsSqliteForLocalDevelopment()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:DefaultConnection"] = "Data Source=diettracker.db"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStorageInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();

        Assert.NotNull(db);
        Assert.True(db.Database.IsSqlite());
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", db.Database.ProviderName);
    }

    [Fact]
    public void AddStorageInfrastructure_AzureSqlWithoutConnectionString_ThrowsInvalidOperationException()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "AzureSql"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddStorageInfrastructure(configuration));
        Assert.Contains("Database provider is configured as 'AzureSql' or 'SqlServer', but no connection string was found", ex.Message);
    }

    [Fact]
    public void AddStorageInfrastructure_SqlServerAuth_EnforcesMinimum60SecondsConnectTimeoutForColdStart()
    {
        const string rawConnStr = "Server=tcp:sql-dietdost-dev.database.windows.net,1433;Initial Catalog=dietdost;User ID=sqladmin;Password=DietDost@Secret2026!;Encrypt=True;TrustServerCertificate=False;Connection Timeout=15;";

        var configData = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "AzureSql",
            ["ConnectionStrings:AzureSqlConnection"] = rawConnStr
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStorageInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DietTrackerDbContext>();

        var connString = db.Database.GetConnectionString();
        Assert.NotNull(connString);
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connString);
        Assert.True(builder.ConnectTimeout >= 60, $"Expected ConnectTimeout >= 60, got {builder.ConnectTimeout}");
        Assert.Equal("sqladmin", builder.UserID);
        Assert.Equal("dietdost", builder.InitialCatalog);
    }
}

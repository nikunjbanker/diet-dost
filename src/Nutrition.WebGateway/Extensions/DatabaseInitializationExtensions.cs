/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Domain.Model.Progress;
using Nutrition.Domain.Model.Security;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;
using Nutrition.WebGateway.Initialization;
using Nutrition.WebGateway.Initialization.Abstractions;
using Nutrition.WebGateway.Initialization.Initializers;
using Nutrition.WebGateway.Initialization.Seeders;

namespace Nutrition.WebGateway.Extensions;

/// <summary>
/// Facade providing application startup entrypoints and backwards-compatible hooks for database schema
/// initialization and deterministic data seeding across SQLite, Azure SQL, and Azure DocumentDB.
/// Concrete responsibilities are encapsulated in dedicated OOP strategies and seeders under Nutrition.WebGateway.Initialization.
/// </summary>
public static class DatabaseInitializationExtensions
{
    /// <summary>
    /// Default seed photo reference for sample lunch thali. Verified by contract tests.
    /// </summary>
    public const string DefaultSampleLunchPhoto = "/uploads/meals/sample_lunch_thali.jpg";

    /// <summary>
    /// Primary application startup coordinator initializing schema and executing ordered seeders.
    /// </summary>
    public static async Task InitializeAndSeedDatabaseAsync(this IApplicationBuilder app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        using var scope = app.ApplicationServices.CreateScope();
        var initService = scope.ServiceProvider.GetRequiredService<DatabaseInitializationService>();
        await initService.InitializeAndSeedAsync();
    }

    /// <summary>
    /// Backwards-compatible facade executing SQLite pristine bootstrapping, PRAGMA setup, and incremental migrations.
    /// </summary>
    public static async Task EnsureDatabaseCreatedAndMigratedAsync(DietTrackerDbContext db, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (db.Database.IsSqlServer())
        {
            await EnsureSqlServerDatabaseMigratedAsync(db, logger);
            return;
        }

        var initializer = new SqliteDatabaseInitializer(db, WrapLogger<SqliteDatabaseInitializer>(logger));
        await initializer.InitializeSchemaAsync();
    }

    /// <summary>
    /// Backwards-compatible facade executing Azure SQL schema verification, table reconciliation, and quota resilience.
    /// </summary>
    public static async Task EnsureSqlServerDatabaseMigratedAsync(DietTrackerDbContext db, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        var initializer = new AzureSqlDatabaseInitializer(db, WrapLogger<AzureSqlDatabaseInitializer>(logger));
        await initializer.EnsureSqlServerDatabaseMigratedAsync(default);
    }

    /// <summary>
    /// Backwards-compatible facade executing Azure DocumentDB ping warmup, index creation, and collection seeding.
    /// </summary>
    public static async Task EnsureDocumentDbCreatedAndMigratedAsync(
        IMongoDatabase mongoDatabase,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IAppEnvironment appEnv,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(mongoDatabase);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(appEnv);

        var initializer = new DocumentDbDatabaseInitializer(mongoDatabase, WrapLogger<DocumentDbDatabaseInitializer>(logger));
        await initializer.InitializeSchemaAsync();

        var seeder = CreateOrResolveDataSeeder(serviceProvider, configuration, appEnv, logger, dbContext: null);
        await seeder.SeedAsync();

        var mediaSeeder = new StorageMediaMirrorSeeder(
            serviceProvider,
            serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<Nutrition.Application.Common.Options.StorageOptions>>() ?? Microsoft.Extensions.Options.Options.Create(new Nutrition.Application.Common.Options.StorageOptions()),
            WrapLogger<StorageMediaMirrorSeeder>(logger));
        await mediaSeeder.SeedAsync();
    }

    /// <summary>
    /// Backwards-compatible facade seeding or updating 5-tier demo accounts and anonymous user records.
    /// </summary>
    public static async Task SeedDemoUsersAsync(
        DietTrackerDbContext db,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IAppEnvironment appEnv,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(appEnv);

        var seeder = CreateOrResolveDataSeeder(serviceProvider, configuration, appEnv, logger, db);
        await seeder.SeedAsync();
    }

    /// <summary>
    /// Backwards-compatible facade bootstrapping root SuperAdmin account and clinical daily ledger.
    /// </summary>
    public static async Task EnsureSuperAdminAccountAsync(
        DietTrackerDbContext db,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(configuration);

        var appEnv = serviceProvider.GetService<IAppEnvironment>() ?? new DefaultAppEnvironment();
        var seeder = CreateOrResolveDataSeeder(serviceProvider, configuration, appEnv, logger, db);
        await seeder.SeedAsync();
    }

    private static IDatabaseSeeder CreateOrResolveDataSeeder(
        IServiceProvider sp,
        IConfiguration configuration,
        IAppEnvironment appEnv,
        ILogger? logger,
        DietTrackerDbContext? dbContext)
    {
        var resolvedSeeder = sp.GetService<DefaultDataSeeder>();
        if (resolvedSeeder != null)
        {
            return resolvedSeeder;
        }

        var tierRepo = sp.GetService<IRepository<TierFeatureConfiguration>>()
            ?? (dbContext != null ? new EfRepository<TierFeatureConfiguration>(dbContext, WrapLogger<EfRepository<TierFeatureConfiguration>>(logger)) : null)
            ?? throw new InvalidOperationException("Could not resolve IRepository<TierFeatureConfiguration>.");

        var userRepo = sp.GetService<IRepository<ApplicationUser>>()
            ?? (dbContext != null ? new EfRepository<ApplicationUser>(dbContext, WrapLogger<EfRepository<ApplicationUser>>(logger)) : null)
            ?? throw new InvalidOperationException("Could not resolve IRepository<ApplicationUser>.");

        var profileRepo = sp.GetService<IRepository<UserProfile>>()
            ?? (dbContext != null ? new EfRepository<UserProfile>(dbContext, WrapLogger<EfRepository<UserProfile>>(logger)) : null)
            ?? throw new InvalidOperationException("Could not resolve IRepository<UserProfile>.");

        var mealRepo = sp.GetService<IRepository<MealLog>>()
            ?? (dbContext != null ? new EfRepository<MealLog>(dbContext, WrapLogger<EfRepository<MealLog>>(logger)) : null)
            ?? throw new InvalidOperationException("Could not resolve IRepository<MealLog>.");

        var photoRepo = sp.GetService<IRepository<ProgressPhoto>>()
            ?? (dbContext != null ? new EfRepository<ProgressPhoto>(dbContext, WrapLogger<EfRepository<ProgressPhoto>>(logger)) : null)
            ?? throw new InvalidOperationException("Could not resolve IRepository<ProgressPhoto>.");

        var secretRepo = sp.GetService<IRepository<AppSecret>>()
            ?? (dbContext != null ? new EfRepository<AppSecret>(dbContext, WrapLogger<EfRepository<AppSecret>>(logger)) : null)
            ?? throw new InvalidOperationException("Could not resolve IRepository<AppSecret>.");

        var unitOfWork = sp.GetService<IUnitOfWork>()
            ?? (dbContext != null ? new EfUnitOfWork(dbContext, WrapLogger<EfUnitOfWork>(logger)) : null)
            ?? throw new InvalidOperationException("Could not resolve IUnitOfWork.");

        var passwordHasher = sp.GetRequiredService<IPasswordHasher>();
        var dietitian = sp.GetRequiredService<ClinicalDietitianService>();

        return new DefaultDataSeeder(
            tierRepo,
            userRepo,
            profileRepo,
            mealRepo,
            photoRepo,
            secretRepo,
            unitOfWork,
            passwordHasher,
            dietitian,
            configuration,
            appEnv,
            WrapLogger<DefaultDataSeeder>(logger));
    }

    private static ILogger<T> WrapLogger<T>(ILogger? logger)
    {
        if (logger is ILogger<T> typed) return typed;
        return new LoggerAdapter<T>(logger);
    }

    private sealed class LoggerAdapter<T>(ILogger? inner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner?.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => inner?.IsEnabled(logLevel) ?? false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => inner?.Log(logLevel, eventId, state, exception, formatter);
    }

    private sealed class DefaultAppEnvironment(bool isDevelopment = true) : IAppEnvironment
    {
        public bool IsDebugMode => true;
        public bool IsDevelopment => isDevelopment;
    }
}

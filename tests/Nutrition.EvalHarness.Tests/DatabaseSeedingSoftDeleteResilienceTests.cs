/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Common;
using Nutrition.Application.Common.Interfaces;
using Nutrition.Application.Common.Options;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;
using Nutrition.Infrastructure.Persistence;
using Nutrition.Infrastructure.Security;
using Nutrition.WebGateway.Extensions;
using Xunit;

namespace Nutrition.EvalHarness.Tests;

/// <summary>
/// Verifies that database seeding logic (SeedDemoUsersAsync, EnsureSuperAdminAccountAsync)
/// is completely idempotent and resilient against soft-deleted records and global query filters,
/// preventing SQL Server/SQLite duplicate key constraint violations (PK_Users, PK_Profiles).
/// </summary>
[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Test harness isolated database name generated via GUID")]
public class DatabaseSeedingSoftDeleteResilienceTests
{
    private sealed class TestAppEnvironment : IAppEnvironment
    {
        public bool IsDebugMode => true;
        public bool IsDevelopment => true;
        public bool AllowsDemoUsers => true;
        public bool AllowsAdminDemoUsers => true;
    }

    private static (ServiceProvider ServiceProvider, DietTrackerDbContext Db, string DbPath) CreateTestContext()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"dietdost_seed_resilience_{Guid.NewGuid():N}.db");
        var connStr = $"Data Source={tempDbPath}";

        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Auth:SuperAdminEmail"] = "nikunj_banker@hotmail.com",
            ["Auth:SuperAdminPassword"] = "DietDost@Demo2026!",
            ["Auth:DemoPassword"] = "DietDost@Demo2026!",
            ["Auth:TermsVersion"] = "v1.0-202609",
            ["Auth:HealthConsentVersion"] = "v1.0-202609",
            ["Database:Provider"] = "Sqlite"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IAppEnvironment, TestAppEnvironment>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        services.AddDbContext<DietTrackerDbContext>(options =>
            options.UseSqlite(connStr));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ClinicalDietitianService>();

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<DietTrackerDbContext>();

        return (provider, db, tempDbPath);
    }

    [Fact]
    public async Task SeedDemoUsers_WhenAnonymousUserSoftDeleted_ReactivatesWithoutDuplicateKeyViolation()
    {
        var (provider, db, dbPath) = CreateTestContext();
        try
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var appEnv = provider.GetRequiredService<IAppEnvironment>();

            // 1. Initial schema creation and first seed run
            await DatabaseInitializationExtensions.EnsureDatabaseCreatedAndMigratedAsync(db, NullLogger.Instance);
            await DatabaseInitializationExtensions.SeedDemoUsersAsync(db, provider, config, appEnv, NullLogger.Instance);

            // Verify anonymous user exists and is active
            var initialAnon = await db.Users.FirstOrDefaultAsync(u => u.Id == "usr_87c369c1e3f8");
            Assert.NotNull(initialAnon);
            Assert.False(initialAnon.IsDeleted);
            Assert.True(initialAnon.IsActive);

            var initialProfile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == "usr_87c369c1e3f8");
            Assert.NotNull(initialProfile);
            Assert.False(initialProfile.IsDeleted);

            // 2. Simulate soft-deletion of anonymous user and profile (e.g. via SuperAdmin deletion action)
            initialAnon.IsDeleted = true;
            initialAnon.DeletedAtUtc = DateTime.UtcNow;
            initialAnon.IsActive = false;
            initialProfile.IsDeleted = true;
            initialProfile.DeletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Assert that normal EF Core queries now filter them out due to HasQueryFilter(!e.IsDeleted)
            var filteredAnon = await db.Users.FirstOrDefaultAsync(u => u.Id == "usr_87c369c1e3f8");
            Assert.Null(filteredAnon);
            var filteredProfile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == "usr_87c369c1e3f8");
            Assert.Null(filteredProfile);

            // 3. Second seed run (simulating application/container reboot/deployment)
            // MUST NOT throw DbUpdateException or PRIMARY KEY constraint violation!
            var exception = await Record.ExceptionAsync(() =>
                DatabaseInitializationExtensions.SeedDemoUsersAsync(db, provider, config, appEnv, NullLogger.Instance));

            Assert.Null(exception);

            // 4. Verify user was reactivated and restored cleanly
            var restoredAnon = await db.Users.FirstOrDefaultAsync(u => u.Id == "usr_87c369c1e3f8");
            Assert.NotNull(restoredAnon);
            Assert.False(restoredAnon.IsDeleted);
            Assert.True(restoredAnon.IsActive);
            Assert.Null(restoredAnon.DeletedAtUtc);

            var restoredProfile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == "usr_87c369c1e3f8");
            Assert.NotNull(restoredProfile);
            Assert.False(restoredProfile.IsDeleted);
            Assert.Null(restoredProfile.DeletedAtUtc);

            // Assert exact single record exists in raw database
            var totalAnonCount = await db.Users.IgnoreQueryFilters().CountAsync(u => u.Id == "usr_87c369c1e3f8");
            Assert.Equal(1, totalAnonCount);
            var totalProfileCount = await db.Profiles.IgnoreQueryFilters().CountAsync(p => p.Id == "usr_87c369c1e3f8");
            Assert.Equal(1, totalProfileCount);
        }
        finally
        {
            await db.DisposeAsync();
            await provider.DisposeAsync();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task SeedDemoUsers_WhenStandardDemoUsersSoftDeleted_ReactivatesWithoutCollision()
    {
        var (provider, db, dbPath) = CreateTestContext();
        try
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var appEnv = provider.GetRequiredService<IAppEnvironment>();

            await DatabaseInitializationExtensions.EnsureDatabaseCreatedAndMigratedAsync(db, NullLogger.Instance);
            await DatabaseInitializationExtensions.SeedDemoUsersAsync(db, provider, config, appEnv, NullLogger.Instance);

            // Soft delete user-free and its profile
            var freeUser = await db.Users.FirstAsync(u => u.Id == "user-free");
            freeUser.IsDeleted = true;
            freeUser.DeletedAtUtc = DateTime.UtcNow;
            freeUser.IsActive = false;

            var freeProfile = await db.Profiles.FirstAsync(p => p.Id == "user-free");
            freeProfile.IsDeleted = true;
            freeProfile.DeletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Run seeding again: must resurrect user-free and profile without crashing
            var exception = await Record.ExceptionAsync(() =>
                DatabaseInitializationExtensions.SeedDemoUsersAsync(db, provider, config, appEnv, NullLogger.Instance));

            Assert.Null(exception);

            var resurrectedUser = await db.Users.FirstOrDefaultAsync(u => u.Id == "user-free");
            Assert.NotNull(resurrectedUser);
            Assert.False(resurrectedUser.IsDeleted);
            Assert.True(resurrectedUser.IsActive);

            var resurrectedProfile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == "user-free");
            Assert.NotNull(resurrectedProfile);
            Assert.False(resurrectedProfile.IsDeleted);

            var count = await db.Users.IgnoreQueryFilters().CountAsync(u => u.Id == "user-free");
            Assert.Equal(1, count);
        }
        finally
        {
            await db.DisposeAsync();
            await provider.DisposeAsync();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task EnsureSuperAdmin_WhenSuperAdminSoftDeleted_RestoresSuperAdminCleanly()
    {
        var (provider, db, dbPath) = CreateTestContext();
        try
        {
            var config = provider.GetRequiredService<IConfiguration>();

            await DatabaseInitializationExtensions.EnsureDatabaseCreatedAndMigratedAsync(db, NullLogger.Instance);
            await DatabaseInitializationExtensions.EnsureSuperAdminAccountAsync(db, provider, config, NullLogger.Instance);

            // Soft-delete superadmin-root
            var superAdmin = await db.Users.FirstAsync(u => u.Id == "superadmin-root");
            superAdmin.IsDeleted = true;
            superAdmin.DeletedAtUtc = DateTime.UtcNow;
            superAdmin.IsActive = false;

            var profile = await db.Profiles.FirstAsync(p => p.Id == "superadmin-root");
            profile.IsDeleted = true;
            profile.DeletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Run EnsureSuperAdminAccountAsync again
            var exception = await Record.ExceptionAsync(() =>
                DatabaseInitializationExtensions.EnsureSuperAdminAccountAsync(db, provider, config, NullLogger.Instance));

            Assert.Null(exception);

            var restoredAdmin = await db.Users.FirstOrDefaultAsync(u => u.Id == "superadmin-root");
            Assert.NotNull(restoredAdmin);
            Assert.False(restoredAdmin.IsDeleted);
            Assert.True(restoredAdmin.IsActive);

            var restoredProfile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == "superadmin-root");
            Assert.NotNull(restoredProfile);
            Assert.False(restoredProfile.IsDeleted);

            var count = await db.Users.IgnoreQueryFilters().CountAsync(u => u.Id == "superadmin-root");
            Assert.Equal(1, count);
        }
        finally
        {
            await db.DisposeAsync();
            await provider.DisposeAsync();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task SeedDemoUsers_WhenOrphanProfileExists_ReconcilesWithoutPrimaryKeyViolation()
    {
        var (provider, db, dbPath) = CreateTestContext();
        try
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var appEnv = provider.GetRequiredService<IAppEnvironment>();

            await DatabaseInitializationExtensions.EnsureDatabaseCreatedAndMigratedAsync(db, NullLogger.Instance);

            // Create orphan profile for anonymous user without creating the user
            var orphanProfile = new UserProfile
            {
                Id = "usr_87c369c1e3f8",
                Name = "Old Orphan Profile",
                Sex = BiologicalSex.Male,
                Age = 40,
                HeightCm = 170,
                CurrentWeightKg = 75,
                TargetWeightKg = 70,
                DesiredPaceKgPerWeek = 0.5,
                ActivityLevel = ActivityLevel.Sedentary,
                DietaryPreference = DietaryPreference.LactoVeg,
                RegionalCuisine = "Gujarati",
                Timezone = "Asia/Kolkata",
                DiagnosedConditions = new List<string>(),
                Medications = new List<MedicationEntry>(),
                IsDeleted = true
            };
            db.Profiles.Add(orphanProfile);
            await db.SaveChangesAsync();

            // Seeding when user is null but profile physically exists in database
            // MUST NOT throw PK_Profiles duplicate key violation!
            var exception = await Record.ExceptionAsync(() =>
                DatabaseInitializationExtensions.SeedDemoUsersAsync(db, provider, config, appEnv, NullLogger.Instance));

            Assert.Null(exception);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == "usr_87c369c1e3f8");
            Assert.NotNull(user);

            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == "usr_87c369c1e3f8");
            Assert.NotNull(profile);
            Assert.False(profile.IsDeleted);
            Assert.Equal("Nikunj QR", profile.Name);
        }
        finally
        {
            await db.DisposeAsync();
            await provider.DisposeAsync();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }
}

